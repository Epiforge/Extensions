#!/bin/bash
set -u
cd "$(dirname "$0")/.." || exit 1
label=${1:-}
case "$label" in
    ""|env|suites|probe|ios) echo "usage: bash Epiforge.Extensions.AotProbe/run.sh <label> [env] [suites] [probe] [ios]  (results go to TestResults/Aot/<label>)"; exit 2 ;;
esac
shift
steps=${*:-env suites probe ios}
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

if has_step ios; then
    if [ "$os" != osx ]; then
        note "the iOS step needs macOS with Xcode and the .NET iOS workload; skipped"
        exit 0
    fi
    project=Epiforge.Extensions.AotProbe.iOS
    bundle=com.epiforge.extensions.aotprobe
    simulator=iossimulator-$arch
    udid=$(xcrun simctl list devices available | awk '/^-- iOS/ { section = 1; first = "" } /iPhone/ && section && first == "" { first = $0; chosen = $0 } END { print chosen }' | sed -E 's/.*\(([0-9A-F-]{36})\).*/\1/')
    if [ -z "$udid" ]; then
        note "no available iPhone simulator"
        exit 5
    fi
    note "simulator $udid: $(xcrun simctl list devices available | grep "$udid")"
    xcrun simctl boot "$udid" 2>/dev/null
    xcrun simctl bootstatus "$udid" -b > /dev/null
    for variant in Debug Release Release-interpreter; do
        configuration=${variant%%-*}
        case "$variant" in
            Release-interpreter) extra="-p:UseInterpreter=true" ;;
            *) extra="" ;;
        esac
        note "iOS $variant build"
        rm -rf "$project/bin/$configuration" "$project/obj/$configuration"
        if ! dotnet build "$project" -c "$configuration" -r "$simulator" $extra > "$out/ios-$variant-build.txt" 2>&1; then
            note "iOS $variant build failed; see $out/ios-$variant-build.txt"
            continue
        fi
        app=$(find "$project/bin/$configuration" -type d -name "*.app" | head -1)
        ls -la "$app" > "$out/ios-$variant-app.txt"
        xcrun simctl terminate "$udid" "$bundle" 2>/dev/null
        xcrun simctl uninstall "$udid" "$bundle" 2>/dev/null
        if ! xcrun simctl install "$udid" "$app"; then
            note "iOS $variant install failed"
            echo "install failed" > "$out/probe-ios-$variant.txt"
            continue
        fi
        note "iOS $variant launch"
        xcrun simctl launch "$udid" "$bundle" > /dev/null
        report="$(xcrun simctl get_app_container "$udid" "$bundle" data)/Documents/aot-probe.txt"
        waited=0
        until grep -q '^summary' "$report" 2>/dev/null || [ $waited -ge 300 ]; do
            sleep 2
            waited=$((waited + 2))
        done
        if [ -f "$report" ]; then
            cp "$report" "$out/probe-ios-$variant.txt"
        else
            echo "no report" > "$out/probe-ios-$variant.txt"
        fi
        tail -1 "$out/probe-ios-$variant.txt"
        xcrun simctl terminate "$udid" "$bundle" 2>/dev/null
    done
fi

note "done"
