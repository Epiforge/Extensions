$resultsDirectory = Join-Path $PSScriptRoot 'TestResults'
if (Test-Path $resultsDirectory) {
    Remove-Item $resultsDirectory -Recurse -Force
}
foreach ($testProject in Get-ChildItem -Path $PSScriptRoot -Recurse -Filter '*.Tests.csproj')
{
    Write-Host "Testing $($testProject.BaseName)" -ForegroundColor Cyan
    dotnet test $testProject.FullName --logger "trx;LogFilePrefix=$($testProject.BaseName)" --results-directory $resultsDirectory --blame-hang --blame-hang-timeout 1m --blame-hang-dump-type full
}
$dumps = @(Get-ChildItem -Path $resultsDirectory -Recurse -Filter '*.dmp' -ErrorAction SilentlyContinue)
if ($dumps.Count -gt 0) {
    if (Get-Command dotnet-dump -ErrorAction SilentlyContinue) {
        foreach ($dump in $dumps)
        {
            Write-Host "Analyzing $($dump.Name)" -ForegroundColor Cyan
            dotnet-dump analyze $dump.FullName --command 'clrthreads' --command 'clrstack -all' --command 'dumpasync' --command 'exit' | Out-File -FilePath "$($dump.FullName).txt" -Encoding utf8
        }
    }
    else {
        Write-Host 'Hang dumps were written, but dotnet-dump is not installed: dotnet tool install --global dotnet-dump' -ForegroundColor Yellow
    }
}
