#!/bin/bash
set -u
cd "$(dirname "$0")/.." || exit 1
label=${1:-}
case "$label" in
    ""|env|suites|probe|ios|coreclr) echo "usage: bash Epiforge.Extensions.AotProbe/run.sh <label> [env] [suites] [probe] [ios] [coreclr]  (results go to TestResults/Aot/<label>)"; exit 2 ;;
esac
shift
steps=${*:-env suites probe ios coreclr}
out=TestResults/Aot/$label
mkdir -p "$out"
exec > >(tee -a "$out/run.log") 2>&1
note() { echo "[$(date '+%Y-%m-%d %H:%M:%S')] $*"; }
note "label $label, steps: $steps, commit $(git log -1 --format='%h')"
git status --short

case "$(uname -s)" in
    Darwin) os=osx ;;
    Linux) os=linux ;;
    *) os=unknown ;;
esac
case "$(uname -m)" in
    arm64|aarch64) arch=arm64 ;;
    x86_64|amd64) arch=x64 ;;
    *) arch=unknown ;;
esac

has_step() {
    case " $steps " in *" $1 "*) return 0 ;; *) return 1 ;; esac
}

forget_runtime_config() {
    find "$1/bin" -name "$(basename "$1").runtimeconfig.json" -delete 2>/dev/null
}

installed_frameworks() {
    for major in 6 7 8 9 10; do
        if dotnet --list-runtimes | grep -q "^Microsoft.NETCore.App $major\."; then
            printf 'net%s.0 ' "$major"
        fi
    done
}

if has_step env; then
    note "env"
    {
        uname -a; echo
        dotnet --info; echo
        dotnet workload list; echo
        if [ "$os" = osx ]; then
            sw_vers; echo
            xcode-select -p; xcodebuild -version; echo
            xcrun simctl list runtimes; echo
            xcrun simctl list devices available
        fi
    } > "$out/env.txt" 2>&1
fi

if has_step suites; then
    frameworks=$(installed_frameworks)
    note "frameworks with an installed runtime: $frameworks"
    for project in Components Collections Expressions Components.WithoutDynamicCode Expressions.WithoutDynamicCode; do
        name=Epiforge.Extensions.$project.Tests
        targets=$(grep -o '<TargetFrameworks>[^<]*' "$name/$name.csproj" | sed 's/<TargetFrameworks>//; s/;/ /g')
        for tfm in $targets; do
            case " $frameworks " in *" $tfm "*) ;; *) continue ;; esac
            note "tests $project $tfm"
            dotnet test "$name" -c Release -f "$tfm" --logger "trx;LogFilePrefix=$project" --results-directory "$out/trx" --blame-hang-timeout 2m --blame-hang-dump-type none > "$out/tests-$project-$tfm.txt" 2>&1
            grep -E "Passed!|Failed!|Aborted|error [A-Z]+[0-9]+" "$out/tests-$project-$tfm.txt" | head -3
        done
    done
fi

if has_step probe; then
    probe=Epiforge.Extensions.AotProbe
    rid=$os-$arch
    forget_runtime_config "$probe"
    note "probe, JIT, dynamic code on"
    dotnet run --project "$probe" -c Release --property:PublishAot=false -- "$out/probe-jit-on.txt" "console JIT, dynamic code on" > "$out/probe-jit-on-console.txt" 2>&1
    tail -1 "$out/probe-jit-on-console.txt"
    forget_runtime_config "$probe"
    note "probe, JIT, dynamic code off"
    dotnet run --project "$probe" -c Release -- "$out/probe-jit-off.txt" "console JIT, dynamic code off" > "$out/probe-jit-off-console.txt" 2>&1
    tail -1 "$out/probe-jit-off-console.txt"
    forget_runtime_config "$probe"
    note "probe, Native AOT $rid"
    rm -rf "$out/native"
    if dotnet publish "$probe" -c Release -r "$rid" -o "$out/native" > "$out/probe-native-publish.txt" 2>&1; then
        "$out/native/$probe" "$out/probe-native.txt" "Native AOT $rid" > "$out/probe-native-console.txt" 2>&1
        tail -1 "$out/probe-native-console.txt"
    else
        note "Native AOT publish failed; see $out/probe-native-publish.txt"
    fi
fi

ios_project=Epiforge.Extensions.AotProbe.iOS
ios_bundle=com.epiforge.extensions.aotprobe
udid=""

on_apple_host() {
    if [ "$os" != osx ]; then
        note "the iOS steps need macOS with Xcode and the .NET iOS workload; skipped"
        return 1
    fi
    if [ -z "$udid" ]; then
        udid=$(xcrun simctl list devices available | awk '/^-- iOS/ { section = 1; first = "" } /iPhone/ && section && first == "" { first = $0; chosen = $0 } END { print chosen }' | sed -E 's/.*\(([0-9A-F-]{36})\).*/\1/')
        if [ -z "$udid" ]; then
            note "no available iPhone simulator"
            return 1
        fi
        note "simulator $udid: $(xcrun simctl list devices available | grep "$udid")"
        xcrun simctl boot "$udid" 2>/dev/null
        xcrun simctl bootstatus "$udid" -b > /dev/null
    fi
    return 0
}

build_ios() {
    local framework=$1 configuration=$2 log=$3 extra=$4
    rm -rf "$ios_project/bin/$configuration" "$ios_project/obj/$configuration"
    dotnet build "$ios_project" -c "$configuration" -f "$framework" -r "iossimulator-$arch" $extra > "$log" 2>&1
}

built_app() {
    find "$1" -type d -name "*.app" | head -1
}

launch_ios() {
    local app=$1 copy=$2 report waited
    xcrun simctl terminate "$udid" "$ios_bundle" 2>/dev/null
    xcrun simctl uninstall "$udid" "$ios_bundle" 2>/dev/null
    if ! xcrun simctl install "$udid" "$app"; then
        note "install of $app failed"
        echo "install failed" > "$copy"
        return 1
    fi
    xcrun simctl launch "$udid" "$ios_bundle" > /dev/null
    report="$(xcrun simctl get_app_container "$udid" "$ios_bundle" data)/Documents/aot-probe.txt"
    waited=0
    until grep -q '^summary' "$report" 2>/dev/null || [ $waited -ge 600 ]; do
        sleep 2
        waited=$((waited + 2))
    done
    if [ -f "$report" ]; then
        cp "$report" "$copy"
    else
        echo "no report" > "$copy"
    fi
    tail -1 "$copy"
    xcrun simctl terminate "$udid" "$ios_bundle" 2>/dev/null
}

if has_step ios && on_apple_host; then
    for variant in Debug Release Release-interpreter; do
        configuration=${variant%%-*}
        case "$variant" in
            Release-interpreter) extra="-p:UseInterpreter=true" ;;
            *) extra="" ;;
        esac
        note "Mono iOS $variant build"
        if ! build_ios net10.0-ios "$configuration" "$out/ios-$variant-build.txt" "$extra"; then
            note "Mono iOS $variant build failed; see $out/ios-$variant-build.txt"
            continue
        fi
        app=$(built_app "$ios_project/bin/$configuration")
        ls -la "$app" > "$out/ios-$variant-app.txt"
        note "Mono iOS $variant launch"
        launch_ios "$app" "$out/probe-ios-$variant.txt"
    done
fi

if has_step coreclr && on_apple_host; then
    for variant in Debug Release Release-nodynamic; do
        configuration=${variant%%-*}
        case "$variant" in
            Release-nodynamic) extra="-p:DynamicCodeSupport=false" ;;
            *) extra="" ;;
        esac
        rm -rf "$out/$variant"
        mkdir -p "$out/$variant"
        note "CoreCLR iOS $variant build"
        if ! build_ios net11.0-ios "$configuration" "$out/$variant/build.txt" "$extra"; then
            note "CoreCLR iOS $variant build failed; see $out/$variant/build.txt"
            continue
        fi
        app=$(built_app "$ios_project/bin/$configuration")
        find "$app" -type f -exec ls -l {} + > "$out/$variant/app.txt"
        cp -R "$app" "$out/$variant/"
    done
    for launch in 1 2 3; do
        for variant in Debug Release Release-nodynamic; do
            if [ "$variant" = Debug ] && [ $launch -gt 1 ]; then
                continue
            fi
            app=$(find "$out/$variant" -maxdepth 1 -type d -name "*.app" 2>/dev/null | head -1)
            if [ -z "$app" ]; then
                continue
            fi
            note "CoreCLR iOS $variant launch $launch"
            launch_ios "$app" "$out/$variant/probe-$launch.txt"
        done
    done
fi

note "done"
