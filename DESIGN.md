---
name: Worktime
description: Time tracking that reads like station wayfinding signage. Every state is a sign.
colors:
  ground: "#f2f3f1"
  panel: "#e6e8e4"
  card: "#fbfbfa"
  rule: "#cbcfca"
  ink: "#15181c"
  ink-2: "#4a5059"
  ink-3: "#6b717a"
  sign: "#0a4c8a"
  sign-ink: "#ffffff"
  go: "#1d7a45"
  go-deep: "#155f35"
  wait: "#f2b400"
  back: "#d9600f"
  stop: "#c8102e"
  stop-deep: "#a00d25"
  idle: "#8a9099"
typography:
  display:
    fontFamily: "Separator Dot, Overpass Variable, Segoe UI, system-ui, sans-serif"
    fontSize: "clamp(1.875rem, 4vw, 2.25rem)"
    fontWeight: 800
    lineHeight: 1.25
    letterSpacing: "-0.015em"
  headline:
    fontFamily: "Separator Dot, Overpass Variable, Segoe UI, system-ui, sans-serif"
    fontSize: "1.5rem"
    fontWeight: 800
    lineHeight: 1.33
    letterSpacing: "-0.015em"
  title:
    fontFamily: "Separator Dot, Overpass Variable, Segoe UI, system-ui, sans-serif"
    fontSize: "1.125rem"
    fontWeight: 700
    lineHeight: 1.4
    fontFeature: "\"tnum\" 1, \"lnum\" 1"
  body:
    fontFamily: "Separator Dot, Overpass Variable, Segoe UI, system-ui, sans-serif"
    fontSize: "1rem"
    fontWeight: 400
    lineHeight: 1.5
    fontFeature: "\"tnum\" 1, \"lnum\" 1"
  label:
    fontFamily: "Separator Dot, Overpass Variable, Segoe UI, system-ui, sans-serif"
    fontSize: "0.75rem"
    fontWeight: 750
    lineHeight: 1.1
    letterSpacing: "0.06em"
  field-label:
    fontFamily: "Separator Dot, Overpass Variable, Segoe UI, system-ui, sans-serif"
    fontSize: "0.8125rem"
    fontWeight: 650
  small:
    fontFamily: "Separator Dot, Overpass Variable, Segoe UI, system-ui, sans-serif"
    fontSize: "0.875rem"
    fontWeight: 400
    lineHeight: 1.43
rounded:
  plate: "2px"
  panel: "4px"
spacing:
  xs: "4px"
  sm: "8px"
  md: "12px"
  lg: "16px"
  xl: "24px"
  2xl: "32px"
  3xl: "48px"
components:
  button-primary:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.ground}"
    rounded: "{rounded.plate}"
    padding: "8px 16px 6px"
    height: "40px"
  button-primary-hover:
    backgroundColor: "#000000"
  button-quiet:
    backgroundColor: "transparent"
    textColor: "{colors.ink}"
    rounded: "{rounded.plate}"
    padding: "8px 16px 6px"
    height: "40px"
  button-quiet-hover:
    backgroundColor: "{colors.card}"
  button-go:
    backgroundColor: "{colors.go}"
    textColor: "#ffffff"
    rounded: "{rounded.plate}"
    height: "64px"
  button-go-hover:
    backgroundColor: "{colors.go-deep}"
  button-stop:
    backgroundColor: "{colors.stop}"
    textColor: "#ffffff"
    rounded: "{rounded.plate}"
    padding: "8px 16px 6px"
  button-stop-hover:
    backgroundColor: "{colors.stop-deep}"
  button-sm:
    padding: "6px 12px 4px"
    height: "32px"
  input:
    backgroundColor: "{colors.card}"
    textColor: "{colors.ink}"
    rounded: "{rounded.plate}"
    padding: "8px 12px 6px"
    height: "40px"
  plate-pending:
    backgroundColor: "{colors.wait}"
    textColor: "{colors.ink}"
    typography: "{typography.label}"
    rounded: "{rounded.plate}"
    padding: "4px 8px 2px"
  plate-revision:
    backgroundColor: "{colors.back}"
    textColor: "#ffffff"
    typography: "{typography.label}"
    rounded: "{rounded.plate}"
    padding: "4px 8px 2px"
  plate-approved:
    backgroundColor: "{colors.go}"
    textColor: "#ffffff"
    typography: "{typography.label}"
    rounded: "{rounded.plate}"
    padding: "4px 8px 2px"
  plate-rejected:
    backgroundColor: "{colors.stop}"
    textColor: "#ffffff"
    typography: "{typography.label}"
    rounded: "{rounded.plate}"
    padding: "4px 8px 2px"
  plate-online:
    backgroundColor: "{colors.sign}"
    textColor: "{colors.sign-ink}"
    typography: "{typography.label}"
    rounded: "{rounded.plate}"
    padding: "4px 8px 2px"
  plate-outline:
    backgroundColor: "transparent"
    textColor: "{colors.ink-2}"
    typography: "{typography.label}"
    rounded: "{rounded.plate}"
    padding: "4px 8px 2px"
  header-band:
    backgroundColor: "{colors.ink}"
    textColor: "{colors.ground}"
    height: "56px"
  dialog:
    backgroundColor: "{colors.card}"
    rounded: "{rounded.panel}"
    padding: "24px"
---

# Design System: Worktime

## Overview

**Creative North Star: "The Station Board"**

Worktime is built like a railway station's wayfinding programme (the SBB / Aicher lineage): one system, legible at ten metres, never ambiguous. Ink sits on a cool, slightly grey ground; thin rules frame zones the way a departures board frames its rows. Colour is not decoration. It is reserved for flat enamel plates that announce a state, and for the few actions that change one.

Density is moderate and calm. Screens are rows, rules and plates, not cards in a dashboard grid. Numbers are always tabular so times line up like a timetable. The one piece of authored motion is the station clock's red second hand, which sweeps the dial in 58.5 seconds and rests at twelve until the minute hand jumps. Time is drawn as length: a log on the day track is exactly as wide as the time it covers.

The system rejects the category's default look: sidebar, KPI cards, indigo tables.

**Key Characteristics:**
- Ink on cool ground, 1px rules as the main structural device.
- Colour only on enamel plates, the punch action, destructive confirmations and the clock's second hand.
- Plates: flat, 2px corners, uppercase, tracked, always pictogram or dot plus word.
- Overpass (Highway Gothic lineage) at heavy weights, tabular lining numerals everywhere.
- Authored 2px square-cap pictograms on a 24-unit grid.
- Duration is length; the open shift is a signed row, not an empty state.

## Colors

A neutral signage palette: four cool greys and an ink, one information blue, and four enamel state colours that never appear as decoration.

### Primary
- **Information Blue** (sign): navigation links, text links, focus rings, selection, form accent and caret, the "En línea" presence plate. It is wayfinding, never state.

### Secondary (state enamels)
- **Signal Green** (go): approved logs, working presence, live connection, the punch-in action, the open segment on the day track.
- **Platform Yellow** (wait): pending logs, "por decidir" counts, reconnecting, the no-supervisor notice. Always carries ink text, never white.
- **Return Orange** (back): needs revision, back to the worker.
- **Stop Red** (stop): rejected logs, punch-out, destructive confirmations (reject, deactivate, dismiss), offline connection, the clock's second hand and the "now" line on the day track.
- **Idle Grey** (idle): offline presence dot only.

### Neutral
- **Ground** (ground): page background; also the text colour on the ink header band and ink buttons.
- **Panel** (panel): recessed surfaces: the day track bed, month-day bars, skeletons, scrollbar track.
- **Card** (card): raised paper: team member cells, inputs, the clock face, dialogs, the mobile bottom nav.
- **Rule** (rule): every 1px divider, outline and inset input stroke.
- **Ink** (ink): text, the header band, primary buttons, clock hands and ticks.
- **Ink 2** (ink-2): secondary text, field labels, metadata.
- **Ink 3** (ink-3): tertiary text: placeholders, hour labels on the track, weekday headers, inactive bottom tabs.

### Named Rules
**The Enamel Rule.** State colour lives only on plates. Buttons that decide (Aprobar) are ink; only the punch action and destructive confirmations take enamel. A plate is never painted for emphasis.

**The Zero Is Outlined Rule.** A total, plate or status with nothing in it is drawn as a 1px rule outline in ink-3, not as an enamel fill. Colour means something is there.

**The Yellow Carries Ink Rule.** Platform Yellow always takes ink text; white on yellow is never used.

## Typography

**Display Font:** Overpass Variable (with Segoe UI, system-ui)
**Body Font:** Overpass Variable (same family)
**Separator glyph:** the middle dot (U+00B7) is borrowed from Segoe UI / Helvetica Neue / Arial via a unicode-range face, because Overpass draws it flush right.

**Character:** One road-sign grotesque at two temperatures: extra-bold for signs and figures, regular for explanation. Tabular lining numerals are on globally (`tnum`, `lnum`) so every time and total aligns.

### Hierarchy
- **Display** (800, 1.875rem to 2.25rem, tight): the login statement only.
- **Headline** (800, 1.5rem rising to 1.875rem at sm, -0.015em): page titles and the day name on Hoy; first letter uppercased for locale date strings.
- **Title** (700 to 800, 1.125rem to 1.25rem): row times ("08:12–16:40"), member names, group headings, dialog titles, the running total.
- **Body** (400, 1rem, 1.5): explanations, empty states, notes. Secondary lines drop to 0.875rem in ink-2.
- **Label** (750, 0.75rem, 0.06em, uppercase): plates, month total labels, weekday and matrix column headers. Never used as a standalone kicker above a heading.
- **Field label** (650, 0.8125rem, ink-2, sentence case): form field captions.

### Named Rules
**The Timetable Rule.** Every number that is a time, duration or count is tabular. Times are written start–end with an en dash.

**The Labelled Once Rule.** Nothing is labelled twice. A heading is not preceded by an eyebrow, a plate is not followed by the same word in prose, an icon does not repeat its label.

## Layout

A single centered column, max 72rem (6xl), with 16px side padding rising to 24px at sm. The ink header band is sticky at 56px. On md and up, navigation lives in the header as tabs; below md it moves to a fixed bottom bar on card with a top rule, and main gets 112px bottom padding to clear it.

The worker's Hoy view is a 5:7 split at lg (clock and punch left, the day right) with 48px gutters, stacking to clock-on-top below. Month view inverts to 7:5 (calendar left, selected day right). Team uses a ruled grid of cells, 1, 2 and 3 columns at base, sm and lg, built from shared 1px borders rather than gaps. Lists are rows divided by rules (divide-y), never floating cards.

Spacing rhythm: 12px inside rows and between controls, 16px for cell padding, 24px between a heading and its content, 32 to 48px between zones, 40px before group headings.

## Elevation & Depth

Flat by default. Depth is conveyed by tone (panel recessed, card raised against ground) and by 1px rules. One shadow exists and it means "this floats above the page": dialogs, the sticky batch-action bar in the inbox, the sticky unsaved-changes bar in the matrix, and the hover state of ink buttons.

### Shadow Vocabulary
- **Lift** (`0 1px 2px rgb(21 24 28 / 0.08), 0 4px 12px rgb(21 24 28 / 0.06)`): dialogs, sticky ink action bars, ink button hover.
- **Rule outline** (`inset 0 0 0 1px var(--color-rule)`): not elevation but the outline device for quiet buttons, inputs, empty totals, offline plates and notice boxes. Inputs and quiet buttons darken it to ink-3 on hover; focus thickens it to 2px blue.

### Named Rules
**The Flat Board Rule.** Surfaces at rest have no shadow. Lift only for things that genuinely float over content.

## Shapes

Near-square. Plates, buttons, inputs, track segments, notices and calendar grids use a 2px corner, the bend of a pressed-enamel sign. Dialogs alone use 4px. Full circles are reserved for the clock face, presence and connection dots. Strokes are 1px rules; emphasis strokes are 3px (active tab bars, focus outline) and the ink underline beneath team group headings.

Pictograms are drawn on a 24-unit grid, 2px stroke, square caps and mitred joins, no fills, sized 11 to 22px.

## Components

### Buttons
Solid, compact, sign-like.
- **Shape:** 2px corners; 40px min height (32px small, 64px for the punch action); optical padding with 2px more on top than bottom to centre Overpass caps.
- **Primary (ink):** ink fill, ground text, weight 700. The default for every decision, including Aprobar.
- **Hover / Focus:** hover goes to pure black with Lift; active nudges down 1px; 150ms expo-out. Focus is the global 3px blue outline at 2px offset. Disabled is 45% opacity.
- **Quiet:** transparent with an inset rule outline; for secondary choices (Corrección, Rechazar before confirm, Corregir, Historial, Cancelar).
- **Go / Stop:** enamel buttons, only for the punch action (Marcar entrada / Marcar salida) and for confirming a destructive step (reject, deactivate, dismiss).

### Plates (signature)
The one carrier of state colour.
- **Style:** inline-flex, 2px corners, label type (0.75rem, 750, uppercase, 0.06em), 6px gap between mark and word.
- **Status plates:** Pending (yellow + clock), Needs revision (orange + back arrow), Approved (green + check), Rejected (red + cross). Always pictogram plus word.
- **Presence plates:** Working (green, pulsing white dot, "Trabajando desde 08:12"), Online (blue, white dot), Offline (rule outline, hollow idle dot, ink-2 text).
- **Neutral plates:** ink-outlined plates for non-state tags (Manual source, role).
- **Connection plate:** the same three-state grammar in the header (En vivo / Reconectando / Sin conexión); the word collapses to screen-reader only on mobile, leaving the dot.

### Rows
- Logs, inbox items and matrix permissions are rows separated by 1px rules. A log row reads time range (title type), duration and source, status plate, then quiet actions.
- **The open shift** is today's first row: "08:12–en curso" with a green Trabajando plate, not a banner or empty state.

### Inputs / Fields
- **Style:** card fill, inset 1px rule stroke, 2px corners, 40px min height; caption above in field-label type.
- **Focus:** stroke becomes 2px Information Blue, no outer glow.
- **Error:** stroke becomes 2px Stop Red via `aria-invalid`.
- **Segmented choice:** outlined options that fill ink when checked.

### Navigation
- **Header band:** ink, 56px, sticky. Wordmark (mini station clock + "Worktime" in 800), then signed tabs: pictogram + word, 700 at 0.9375rem, ground at 72% opacity; active and hover go to full ground; active carries a 3px ground bar on the bottom edge.
- **Mobile bottom nav:** card bar with top rule, equal tabs with 22px pictogram over an 11px label; inactive ink-3, active ink with a 3px ink bar on the top edge.

### Station Clock (signature)
SVG dial: card face, 4px ink ring, 60 ink ticks (hour ticks heavy), rectangular ink hour and minute hands, red second hand with a square bob. The second hand sweeps in 58.5 s and rests at twelve; the minute hand jumps once a minute. Reduced motion steps it once per second. Sized 208 to 288px on Hoy, smaller on login.

### Day Track (signature)
A 40px panel bed spanning 00–24 h, with rule ticks every 3 h and hour labels at 00/06/12/18/24 in ink-3. Each log is a 2px-cornered bar filled with its status enamel, as wide as its duration. The open shift is a green bar with a white diagonal hatch growing to now; now is a 2px red line overhanging the bed.

### Permission Matrix cells
Editable cells are plain checkboxes. Locked cells show an ink check, a lock pictogram and "FIJO" in small uppercase ink-2, with a title explaining why. Pending changes surface in a sticky ink bar with Lift.

## Do's and Don'ts

### Do:
- **Do** put state colour only on plates, the punch action, destructive confirmations and the clock's second hand.
- **Do** pair every plate's colour with a pictogram or dot and a word; colour alone never carries state.
- **Do** make decision buttons ink (Aprobar is ink, not green).
- **Do** draw empty or zero totals as a 1px rule outline in ink-3.
- **Do** draw time as length: a duration on a track is exactly as wide as the time it covers.
- **Do** show the open shift as a signed row ("08:12–en curso" + Trabajando plate).
- **Do** separate content with 1px rules and rows; use the ink-underlined heading for groups.
- **Do** keep all numerals tabular and write ranges with an en dash.
- **Do** use the authored 2px square-cap pictograms; add new ones on the same 24-unit grid.

### Don't:
- **Don't** use eyebrow or kicker labels above headings; the heading is the sign.
- **Don't** label anything twice (plate plus matching prose, icon plus duplicate tooltip text, eyebrow plus heading).
- **Don't** put white text on Platform Yellow.
- **Don't** use rounded corners beyond 2px on plates and controls (4px is for dialogs only), and don't make pills.
- **Don't** add shadows to resting surfaces; Lift is for things that float.
- **Don't** build dashboards from KPI cards, sidebars or indigo tables.
- **Don't** use Information Blue for state, or state enamels for navigation.
- **Don't** use icon fonts or emoji as pictograms.
