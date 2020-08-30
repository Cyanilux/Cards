# Cards
See here for preview : https://twitter.com/Cyanilux/status/1296100597893148677<br /><br />
• Uses **CardHandController.cs** (and **Card.cs**) scripts to set position/rotation for a hand of cards based on curve, and allows for interactions with mouse.<br />
• Intended to work in 3D. Doesn't really work that well with UI due to scaling differences. Worldspace UI or Screenspace-Camera might work?<br />
• Can drag to reorder cards in hand. Drag a card outside the hand bounds and it starts tilting/wobbling based on mouse movement velocity.<br />
• Releasing a card outside of the hand will trigger it to be used, and applies a dissolve shader effect.<br />
• Card model, shadergraph and example scene setup included.<br />
• Universal Render Pipeline required for shader to work, though the script should still work in other pipelines.<br />
• Example cards also use Text Mesh Pro, and are rendered using an overlay camera & Forward Renderer feature to override Stencil values. While this isn't required, if the TextMeshPro material is set to use those stencil values (under debug settings on TMP material, Stencil ID 1, Stencil Comp 3 (aka equal)), it allows text to dissolve with the card (though still can show when overlapping with other cards)<br />
• Doesn't really include actual gameplay elements, it's mostly just a controller for the hand of cards. Includes a very basic mana system though, so cards can only be used if there is enough mana.<br />
<br />
@Cyanilux<br />
:)
