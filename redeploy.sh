#!/usr/bin/env bash
# Non-destructive republish of the live dist/ install.
# Publishes the exe and syncs ONLY the built-in KeepDevObjects mod + the UnityExplorer
# catalog stub. It NEVER deletes dist/Library, so any downloaded/user-added mods (and
# UnityExplorer's fetched files) survive a rebuild.
set -e
P="$(cd "$(dirname "$0")" && pwd)"

# refuse if the game or manager is running (files would be locked / half-updated)
if tasklist 2>/dev/null | grep -qiE "BLUE PRINCE|BPModManager"; then
  echo "Close Blue Prince and BPModManager first."; exit 1
fi

echo "Publishing exe..."
( cd "$P/src" && dotnet publish -c Release -o ../dist >/dev/null )
rm -rf "$P/src/bin" "$P/src/obj"

echo "Syncing built-in mod (KeepDevObjects)..."
mkdir -p "$P/dist/Library/KeepDevObjects"
cp "$P/Library/KeepDevObjects/KeepDevObjects.dll" "$P/dist/Library/KeepDevObjects/"
cp "$P/Library/KeepDevObjects/modinfo.json"       "$P/dist/Library/KeepDevObjects/"

# UnityExplorer: only lay down the catalog stub if it isn't present at all.
# Do NOT overwrite it if it's already been downloaded (that would drop its files map).
if [ ! -f "$P/dist/Library/UnityExplorer/modinfo.json" ]; then
  echo "Adding UnityExplorer catalog stub..."
  mkdir -p "$P/dist/Library/UnityExplorer"
  cp "$P/Library/UnityExplorer/modinfo.json" "$P/dist/Library/UnityExplorer/"
fi

echo "Done. dist/ updated non-destructively:"
ls -la "$P/dist/BPModManager.exe" | awk '{printf "  exe: %.0f MB\n", $5/1048576}'
find "$P/dist/Library" -name modinfo.json | sed "s|$P/dist/||"
