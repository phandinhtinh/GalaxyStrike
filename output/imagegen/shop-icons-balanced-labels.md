# Balanced icon captions

Built-in image_gen edits, transparent_background=true. Replaced Assets/Art/UI/Icons/Shop/{Gift,StarterPack,BattlePass,SpecialOffer}.png while preserving metadata.

Prompt template:
Use case: precise-object-edit. Refine the supplied transparent game sidebar icon. The bottom lettering is too huge. Change ONLY its typography and placement: reduce label cap height to 10% of the square canvas (about 125 pixels on a 1254 canvas), using normal-proportioned bold sans-serif, not tall condensed display lettering. White flat face, thin dark navy outline, no thick 3D extrusion. A discreet UI caption, not a logo. Center the label directly at the bottom of the object with no gap, barely touching its base. Keep the exact same icon object artwork, size, colors and shading; restore any object detail previously hidden by the oversized text. Keep entire asset within safe padding on genuinely transparent background. The label width must follow the modest fixed letter height, NEVER stretch to fill canvas. Exact single-line text: "<label>".

Trial labels: Gift (25% canvas width); Starter Pack (65%); Battle Pass (60%); Special Offer (65%). These trials were too small in the scene and were discarded.

Final selection retains the previously baked StarterPack, BattlePass and SpecialOffer. Gift uses exec-ef55e8c4-5236-4b92-9aae-f2d26dad1c16.png with this final edit prompt:

Edit this transparent game icon. Keep the gift box artwork completely unchanged. Enlarge ONLY the small word "Gift" to about DOUBLE its current cap height, about 18 percent of the entire canvas height, and about 45 percent of the entire canvas width. Center it at the foot of the gift box touching its base, with modest overlap. Bold normal-proportioned white sans-serif with dark navy outline. Do not make giant title typography. Preserve exact text "Gift", transparent background and square canvas.
