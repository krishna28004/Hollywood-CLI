#!/bin/sh
# Run from an extracted, verified Hollywood release archive.
set -eu

fail() { printf '%s\n' "$*" >&2; exit 1; }

case $# in
    0) mode=install ;;
    1) case $1 in --uninstall) mode=uninstall ;; *) fail "Usage: $0 [--uninstall]" ;; esac ;;
    *) fail "Usage: $0 [--uninstall]" ;;
esac

[ "$(id -u)" != 0 ] || fail 'Run this installer as your regular user.'
home=$(CDPATH='' cd -P -- "$HOME" && pwd -P)
data=$home/.local/share
app=$data/hollywood-cli
binary=$app/hollywood
marker=$app/.managed-by-hollywood-cli
lock=$data/.hollywood-cli-install-lock

owns_link() {
    [ -L "$1" ] && [ "$(readlink "$1")" = "$binary" ]
}

owns_install() {
    [ -d "$app" ] && [ ! -L "$app" ] && [ -f "$marker" ] &&
        [ ! -L "$marker" ] && [ "$(cat "$marker")" = 'Hollywood CLI installer v1' ]
}

if [ "$mode" = install ]; then
    src=$(CDPATH='' cd -P -- "$(dirname -- "$0")" && pwd -P)/hollywood
    [ -f "$src" ] && [ -x "$src" ] || fail 'Extract the full archive first. Its hollywood executable is missing or cannot be executed.'
    version=$("$src" --version) || fail 'This executable could not run. Check the OS and architecture of the archive.'
    case $version in 'Hollywood CLI '[0-9]*) ;; *) fail 'The archive executable did not identify itself as Hollywood CLI.' ;; esac
    mkdir -p -- "$data"
else
    [ -d "$data" ] || exit 0
fi

# Reject redirects outside the selected user's home.
real_data=$(CDPATH='' cd -P -- "$data" && pwd -P)
case $real_data in "$home"/*) ;; *) fail 'The Hollywood data directory resolves outside your home.' ;; esac
mkdir -- "$lock" 2>/dev/null || fail 'Another install is active, or a previous install was interrupted. Inspect the install lock in ~/.local/share.'

temp=
fresh=no
created_link=
cleanup() {
    status=$?
    trap - EXIT
    if [ -n "$temp" ]; then rm -f -- "$temp"; fi
    if [ "$fresh" = yes ]; then
        if [ -n "$created_link" ] && owns_link "$created_link"; then rm -f -- "$created_link"; fi
        rm -f -- "$binary" "$marker" "$app/.command-directory" "$app/uninstall.sh"
        rmdir -- "$app" 2>/dev/null || :
    fi
    rmdir -- "$lock" 2>/dev/null || :
    exit "$status"
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
trap 'exit 129' HUP

if [ "$mode" = uninstall ]; then
    owns_install || fail 'No managed Hollywood installation was found.'
    IFS= read -r bin_dir < "$app/.command-directory" || fail 'The installation command directory could not be read.'
    case $bin_dir in "$home"/*) ;; *) fail 'The recorded command directory is outside your home.' ;; esac
    if [ -d "$bin_dir" ]; then
        [ "$(CDPATH='' cd -P -- "$bin_dir" && pwd -P)" = "$bin_dir" ] || fail 'The recorded command directory has moved.'
        for name in hollywood hollywood-cli; do
            if owns_link "$bin_dir/$name"; then rm -- "$bin_dir/$name"; fi
        done
    fi
    rm -f -- "$binary" "$marker" "$app/.command-directory" "$app/uninstall.sh"
    rmdir -- "$app" 2>/dev/null || :
    printf '%s\n' 'Hollywood CLI removed. Your configuration was preserved.'
    exit 0
fi

bin_dir=
if [ -e "$app" ] || [ -L "$app" ]; then
    owns_install || fail "An unmanaged directory or file already exists at $app."
    IFS= read -r bin_dir < "$app/.command-directory" || fail 'The installation command directory could not be read.'
    case $bin_dir in "$home"/*) ;; *) fail 'The recorded command directory is outside your home.' ;; esac
    [ -d "$bin_dir" ] && [ "$(CDPATH='' cd -P -- "$bin_dir" && pwd -P)" = "$bin_dir" ] ||
        fail 'The recorded command directory has moved.'
else
    for candidate in "$home/.local/bin" "$home/bin"; do
        case ":$PATH:" in
            *":$candidate:"*)
                mkdir -p -- "$candidate"
                [ "$(CDPATH='' cd -P -- "$candidate" && pwd -P)" = "$candidate" ] && [ -w "$candidate" ] ||
                    fail "Command directory is not a writable directory at $candidate."
                bin_dir=$candidate
                break ;;
        esac
    done
fi
[ -n "$bin_dir" ] || fail 'No supported user command directory is on PATH. See the install guide for setup, or run ./hollywood from the extracted archive.'
case ":$PATH:" in *":$bin_dir:"*) ;; *) fail "Open a shell with $bin_dir on PATH before reinstalling." ;; esac

command_name=
for name in hollywood hollywood-cli; do
    if owns_link "$bin_dir/$name" || { [ ! -e "$bin_dir/$name" ] && [ ! -L "$bin_dir/$name" ]; }; then
        found=$(command -v "$name" || :)
        if [ -z "$found" ] || [ "$found" = "$bin_dir/$name" ]; then
            command_name=$name
            break
        fi
    fi
done
[ -n "$command_name" ] || fail 'Both hollywood and hollywood-cli are already in use.'
link=$bin_dir/$command_name

if [ ! -d "$app" ]; then
    mkdir -- "$app"
    fresh=yes
    printf '%s\n' 'Hollywood CLI installer v1' > "$marker"
    printf '%s\n' "$bin_dir" > "$app/.command-directory"
    cp -- "$0" "$app/uninstall.sh"
    chmod u+x -- "$app/uninstall.sh"
fi
temp=$(mktemp "$app/.hollywood.XXXXXXXX")
cp -- "$src" "$temp"
chmod 755 -- "$temp"
[ "$("$temp" --version)" = "$version" ] || fail 'The staged executable failed its version check.'
[ ! -d "$binary" ] && [ ! -L "$binary" ] || fail 'The destination executable has been replaced with a directory or symlink.'
# A link to a missing file can safely exist for the short duration of a fresh install.
if ! owns_link "$link"; then
    ln -s -- "$binary" "$link" || fail "Could not install command at $link."
    created_link=$link
fi
if ! mv -f -- "$temp" "$binary"; then
    if [ -n "$created_link" ] && owns_link "$created_link"; then rm -- "$created_link"; fi
    fail 'Could not replace the application executable.'
fi
temp=
fresh=no
printf 'Installed %s. Open a new terminal and type %s.\n' "$version" "$command_name"
