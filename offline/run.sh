#!/bin/sh
cd "$(dirname "$0")/AcierOfflineServer" || exit 1
exec dotnet run -- "$@"
