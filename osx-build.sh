set -xe
dotnet restore -r osx-x64
exec dotnet msbuild \
    -t:BundleApp \
    -p:RuntimeIdentifier=osx-x64 \
    -p:Configuration=Release \
    -p:PublishSingleFile=false \
    -p:PublishTrimmed=true
