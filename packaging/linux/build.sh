#!/usr/bin/env bash
#
# Builds O-view's Linux release assets for one runtime identifier: a .deb and a portable
# tarball (ADR-0010 D5, slicing table row 8).
#
#   ./packaging/linux/build.sh <version> <rid> [output-dir]
#   ./packaging/linux/build.sh 0.6.0 linux-x64 dist
#
# Must run on a Debian-family host: it uses dpkg-deb rather than assembling an ar/tar
# archive by hand, so the result is a package a real install can act on.
#
# The names below are not this script's to choose — ReleaseAssets
# (src/O-view.Core/Updates/ReleaseAssets.cs) already froze them, on the reasoning that the
# workflow writes them and the update checker matches them, so restating them here makes
# them drift and the symptom is an app that quietly stops updating. This script's only job
# is to produce exactly what that file already expects.
set -euo pipefail

VERSION="${1:?usage: build.sh <version> <rid> [output-dir]}"
RID="${2:?usage: build.sh <version> <rid> [output-dir]}"
OUT="${3:-dist}"

case "$RID" in
  linux-x64)   DEB_ARCH=amd64 ;;
  linux-arm64) DEB_ARCH=arm64 ;;
  *) echo "unsupported rid: $RID (expected linux-x64 or linux-arm64)" >&2; exit 1 ;;
esac

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT

mkdir -p "$OUT"
echo "==> building o-view $VERSION for $RID ($DEB_ARCH)"

# ── publish ─────────────────────────────────────────────────────────────────────────
#
# Self-contained deliberately, same reasoning as the Windows installer (ADR-0010 D5): a
# framework-dependent publish would need a dotnet-runtime-10 package that may not exist in
# the user's archive yet, and a tray app that will not start because of a missing runtime
# is indistinguishable from a broken one.
#
# No AssemblyName override exists on O-view.Linux.csproj (confirmed by reading it), so the
# published binary is "O-view.Linux" — the same convention O-view.Tray.csproj uses for
# "O-view.Tray.exe" in installer/O-view.iss.
APP="$STAGE/usr/lib/o-view"
mkdir -p "$APP"
dotnet publish "$ROOT/src/O-view.Linux" \
  --configuration Release \
  --runtime "$RID" \
  --self-contained true \
  -p:Version="$VERSION" \
  -p:DebugType=none \
  --output "$APP"

BINARY_NAME="O-view.Linux"
if [ ! -x "$APP/$BINARY_NAME" ]; then
  echo "expected publish output not found: $APP/$BINARY_NAME" >&2
  exit 1
fi

# .NET publish leaves everything 0744, which makes every .dll look like an executable.
# Normalise: nothing is executable except the one thing that actually is.
find "$APP" -type f -exec chmod 0644 {} +
find "$APP" -type d -exec chmod 0755 {} +
chmod 0755 "$APP/$BINARY_NAME"

# ── package tree ────────────────────────────────────────────────────────────────────

# A shim rather than a symlink into /usr/lib: the single-file host resolves its extraction
# directory from the real path, and a symlinked argv[0] has surprised it before (ported
# comment from the source repository's own build.sh, which hit this).
mkdir -p "$STAGE/usr/bin"
cat > "$STAGE/usr/bin/o-view" <<SHIM
#!/bin/sh
exec /usr/lib/o-view/$BINARY_NAME "\$@"
SHIM
chmod 0755 "$STAGE/usr/bin/o-view"

# Desktop entry. No Icon= line: this repository has no brand/icon assets yet (confirmed by
# directory listing), and a desktop entry naming an icon that is not shipped is worse than
# naming none — the launcher falls back to a generic icon either way, honestly rather than
# pointing at something that does not exist in this build. Add Icon= in the same slice that
# adds the icon files.
mkdir -p "$STAGE/usr/share/applications"
cat > "$STAGE/usr/share/applications/o-view.desktop" <<DESKTOP
[Desktop Entry]
Type=Application
Name=O-view
Comment=Claude usage and time until the next limit reset
Exec=/usr/bin/o-view
Terminal=false
Categories=Utility;Monitor;
Keywords=claude;usage;tokens;monitor;
StartupNotify=false
DESKTOP

# ── control ─────────────────────────────────────────────────────────────────────────
#
# The dependency list is deliberately permissive across releases: ICU and OpenSSL are
# SONAME-versioned differently across Debian/Ubuntu releases. Pinning one would make the
# package uninstallable on the others. The X11/fontconfig entries are Avalonia's, not
# .NET's (same reasoning as the source repository's own control file).
#
# Deliberately no copyright/changelog files and no lintian overrides here: this repository
# has no LICENSE file yet (confirmed by directory listing), and writing Debian packaging
# metadata that names a license this repository has not actually adopted would be
# fabricating a fact, not porting one. Add them in the slice that adds a LICENSE file.
INSTALLED_KB="$(du -sk "$STAGE/usr" | cut -f1)"
mkdir -p "$STAGE/DEBIAN"
cat > "$STAGE/DEBIAN/control" <<CONTROL
Package: o-view
Version: $VERSION
Section: utils
Priority: optional
Architecture: $DEB_ARCH
Maintainer: O-view <noreply@github.com>
Homepage: https://github.com/mlengmark/oview
Installed-Size: $INSTALLED_KB
Depends: libc6, libgcc-s1, libstdc++6, libx11-6, libice6, libsm6, libfontconfig1, libicu74 | libicu72 | libicu71 | libicu70 | libicu69, libssl3t64 | libssl3
Description: Claude usage in the notification area
 O-view shows how much of your Claude plan you have used and how long until
 the next limit resets, from a tray icon and a detail panel.
 .
 It reads only files Claude already writes on this machine. No account, no
 token, and no network access except an optional check for a newer release.
CONTROL

# Deliberately no postrm cleanup of user data, same reasoning as the source repository's own
# build.sh: O-view's state is per-user, a root-run maintainer script cannot know which users
# have it, and removal should leave settings and the weekly-reset log alone.

# ── build ───────────────────────────────────────────────────────────────────────────

find "$STAGE" -type d -exec chmod 0755 {} +
DEB="$OUT/o-view_${VERSION}_${DEB_ARCH}.deb"
dpkg-deb --root-owner-group --build "$STAGE" "$DEB"

# ── tarball ─────────────────────────────────────────────────────────────────────────
#
# For distributions the .deb does not target. Extract and run; no installation, no
# auto-update (ADR-0010 D7 — nothing here installs anything in the background either).
TARDIR="$STAGE/tar/o-view-${VERSION}-${RID}"
mkdir -p "$TARDIR"
cp -a "$APP/." "$TARDIR/"
cat > "$TARDIR/README" <<README
O-view ${VERSION} (${RID})

  ./$BINARY_NAME        run it

Reads only files Claude already writes on this machine. No account, no token.
README

TAR="$OUT/o-view-${VERSION}-${RID}.tar.gz"
tar -czf "$TAR" -C "$STAGE/tar" "o-view-${VERSION}-${RID}"

# ── name check ──────────────────────────────────────────────────────────────────────
#
# Mirrors ReleaseAssets.DebianPackage/Tarball's own predicates (src/O-view.Core/Updates/
# ReleaseAssets.cs) directly against what this run just produced, so a naming mistake here
# fails the build instead of shipping an asset the update checker will silently never
# recognise. tests/O-view.Core.Tests/Updates/ReleaseAssetsTests.cs pins the same shapes from
# the Core side without needing dpkg-deb.
DEB_NAME="$(basename "$DEB")"
case "$DEB_NAME" in
  o-view_*_"$DEB_ARCH".deb) ;;
  *) echo "built .deb name does not match ReleaseAssets.DebianPackage: $DEB_NAME" >&2; exit 1 ;;
esac
TAR_NAME="$(basename "$TAR")"
case "$TAR_NAME" in
  o-view-*-"$RID".tar.gz) ;;
  *) echo "built tarball name does not match ReleaseAssets.Tarball: $TAR_NAME" >&2; exit 1 ;;
esac

echo "==> $DEB"
echo "==> $TAR"
