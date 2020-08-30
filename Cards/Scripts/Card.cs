using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cyan.Cards {
    
    public class Card : MonoBehaviour {

        [Tooltip("Mana required to use card")]
        public int mana;
        
        protected Color color;
        protected Color color2;

        protected MeshRenderer meshRenderer;
        protected Material material;

        protected Vector2 dissolveOffset = new Vector2(0.1f, 0);
        protected Vector2 dissolveSpeed = new Vector2(2f, 2f);
        protected Color dissolveColor;

        protected bool isInactive;
        
        protected virtual void Start() {
            meshRenderer = GetComponentInChildren<MeshRenderer>();
            material = meshRenderer.material; // Create material instance

            color = material.GetColor("_Color");
            color2 = material.GetColor("_OutlineColor");
            dissolveColor = material.GetColor("_DissolveColor");

            // Colour Tests
            /*
            int i = transform.GetSiblingIndex();
            int count = transform.parent.childCount;

            color = Color.HSVToRGB((float)i / count, Random.Range(0.7f, 0.9f), 1f);
            color2 = Color.HSVToRGB((float)i / count + Random.Range(-0.05f, 0.05f), 0.9f, Random.Range(0.6f, 0.8f));
            dissolveColor = Random.ColorHSV(0, 1, 1, 1, 1, 1);

            material.SetColor("_Color", color);
            material.SetColor("_OutlineColor", color2);*/
        }

        /// <summary>
        /// <para>Triggered when the card is used (dragged up then mouse released, with required mana).</para>
        /// <para>This should probably be overriden by a class that inherits this class, to trigger something
        /// or maybe add something here to trigger an event / UnityEvent, etc.</para>
        /// <para>Base applies a dissolve effect, which can be adjusted using dissolveOffset, dissolveSpeed, dissolveColor</para>
        /// </summary>
        public virtual void Use() {
            // Handle Dissolve Effect
            StartCoroutine(Dissolve());
        }

        protected IEnumerator Dissolve() {
            Vector2 t = Vector2.zero - dissolveOffset;
            while (t.x < 1) {
                t.x = (t.x + Time.deltaTime * dissolveSpeed.x);
                if (t.y < 1) {
                    t.y = (t.y + Time.deltaTime * dissolveSpeed.y);
                }
                material.SetVector("_Dissolve", t);
                material.SetColor("_DissolveColor", dissolveColor * 4 * t.y);
                yield return null;
            }
            Destroy(gameObject);
        }

        /// <summary>
        /// Use to swap the card material to an inactiveMaterial. If true should pass in the inactiveMaterial. false resets it to the regular card material so inactiveMaterial can be null.
        /// </summary>
        public virtual void SetInactiveMaterialState(bool isInactive, Material inactiveMaterial = null) {
            if (isInactive == this.isInactive) {
                return; // No change
            }
            this.isInactive = isInactive;
            if (isInactive) {
                // Greyed Out
                /* // Grey out based on original colours (only really works for bright colours, so using separate material instead)
                Color.RGBToHSV(color, out float hue, out float sat, out float val);
                material.SetColor("_Color", Color.HSVToRGB(0, 0, val - 0.2f));

                Color.RGBToHSV(color2, out hue, out sat, out val);
                material.SetColor("_OutlineColor", Color.HSVToRGB(0, 0, val - 0.2f));*/

                // Switch to Inactive Material
                meshRenderer.sharedMaterial = inactiveMaterial;
            } else {
                // Normal Colour
                //material.SetColor("_Color", color);
                //material.SetColor("_OutlineColor", color2);

                // Switch back to normal Material
                meshRenderer.sharedMaterial = material;
            }
        }
        
        public virtual void OnDestroy() {
            if (material != null) Destroy(material);
        }
        
    }

}
