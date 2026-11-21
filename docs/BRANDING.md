# 123Click branding

Transparent PNGs reconstructed from the user-supplied low-resolution logo reference
using the built-in image generation/editing tool (not a new brand design). Final
assets live in src/CurtantControl/Assets/Brand:123click-logo.png (silver/white/red)
and123click-logo-light.png (charcoal/red for a light UI). Alpha is retained; no
background plate is added. These are reconstructions, not original vector masters.

BrandLogo is a shared AXAML view, selecting the image for ActualThemeVariant.
Operating headers, both Settings headers, User/Developer and Razr choosers use it.
Existing CAMERA RAZR badges and action controls remain. Embedded resources mean
small app updates still need only the three managed app files.

Keycheck startup/Stop uses the same white/red PNG, copied by install.sh next to
key_check_new.py. PIL/ImageTk scales it smoothly; missing optional renderer falls
back to Tk subsampling, missing logo to the prior camera vector. Installer includes
python3-pil/python3-pil.imagetk. No button, motor, relay or persistence changes.

## Final edit prompts (built-in tool)

White/red asset:
Create the final clean transparent cutout of the existing brand mark in image1.
Image1 is authoritative for exact logo design, proportions and thin white camera-line
appearance. Image2 is a failed extraction: REMOVE ALL surrounding white speckles,
haloes, clouds, noise, stray pixels; do NOT preserve any of those artifacts. Deliver
just clean thin silver-white line camera with red circular lens and exact text
'123Click' with 'PHOTO BOOTH' below, as in image1. Flat refined thin strokes and the
original bold123/thinClick typography, not thick embossed or beveled metal lettering.
Completely transparent empty pixels outside the actual logo and inside the empty
camera-outline areas; no texture, no shadow, no glow, no checkerboard, no background
at all. Tight horizontal clean professional logo with a small transparent margin.

Light-UI asset:
Edit this exact transparent 123Click PHOTO BOOTH logo into its LIGHT-UI color variant.
Preserve the exact existing layout, size, text, letter shapes, camera outline and red
circular lens. Change all white/silver strokes and lettering to dark charcoal near
#20222B so the logo is legible on a white UI. Keep the red lens red. Do not add or alter
anything else. Retain genuinely transparent alpha everywhere outside actual artwork
and inside the empty camera outline; no opaque background, no plate, no shadow,
no texture, no noise or haloes. Same horizontal aspect ratio and framing as input.


Settings I/II use270x72 tight logo slots so their scaled visible size matches
main's200x52 at the common16:9 desktop aspect ratio. Popup PIL rendering uses the
same artwork viewport(220,148)-(1940,558) and240x60 maximum display, without
modifying source PNGs. Startup and Stop share this single presentation function.


Final size preference0.8.23 supersedes the enlargement above: main150x52 tight
slot, Settings200x54 (scaled-view compensation), chooser150x52 tight, popup
artwork150x40 max. This restores the pre-enlargement User-mode logo as standard.
