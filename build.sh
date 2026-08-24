#!/usr/bin/env sh
set -eu

clean()
{
    printf '\n=== Clean ===\n'
    dotnet clean Icod.Terminal.sln -c Debug
}

restore()
{
    printf '\n=== Restore ===\n'
    dotnet restore Icod.Terminal.sln
}

build()
{
    printf '\n=== Build ===\n'
    dotnet build Icod.Terminal.sln -c Debug --no-restore
}

test()
{
    printf '\n=== Test ===\n'
    dotnet test Icod.Terminal.sln  \
        -c Debug \
        --no-build
}

case "${1-}" in
    "")
        clean
        restore
        build
        test
        ;;

    clean)
        clean
        ;;

    restore)
        restore
        ;;

    build)
        build
        ;;

    test)
        test
        ;;

    *)
        printf 'Invalid section: %s\n' "$1" >&2
        printf 'Usage: %s [clean|restore|build|test]\n' "$0" >&2
        exit 1
        ;;
esac
