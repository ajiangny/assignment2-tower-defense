#!/bin/sh
# fetch_rig.sh <Name> <generation_id> <X-Tos-Date> <sig fbx> <sig diffuse> <sig normal> <sig emissive>
D=${OUT_DIR:-/Users/anjujiang/assignment2/ArtSource/rigging/out_apose}/$1; mkdir -p "$D"
A="X-Tos-Algorithm=TOS4-HMAC-SHA256&X-Tos-Content-Sha256=UNSIGNED-PAYLOAD&X-Tos-Credential=AKLTYzE0MDRlNzUyNWMzNGE5N2JlZTQ5ZGRkZWQ2NWY5ZTA%2F20261109%2Ffile.hyper3d.com%2Ftos%2Frequest&X-Tos-Date=$3&X-Tos-Expires=604800&X-Tos-SignedHeaders=host"
curl -sSf -o "$D/rigged.fbx" "https://file.hyper3d.com/$2/pack/fbx/base_basic_pbr.fbx?response-cache-control=private&response-content-type=application%2Fvnd.autodesk.fbx&response-content-disposition=attachment%3B%20filename%3D%22base_basic_pbr.fbx%22&$A&X-Tos-Signature=$4"
t(){ curl -sSf -o "$D/$1" "https://file.hyper3d.com/$GEN/pack/texture/basic/$1?response-cache-control=private&response-content-type=image%2Fpng&response-content-disposition=attachment%3B%20filename%3D%22$1%22&$A&X-Tos-Signature=$2"; }
GEN=$2
t texture_diffuse.png $5; t texture_normal.png $6; [ -n "$7" ] && t texture_emissive.png $7
ls -la "$D" | awk 'NR>3{print $5, $9}'
