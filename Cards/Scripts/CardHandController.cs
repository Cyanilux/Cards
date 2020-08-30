using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cyan.Cards {

    public class CardHandController : MonoBehaviour {
        
        [Header("Gameplay Settings")]

        [Tooltip("The mana the user starts with / currently has. Will decrease based on cards used.")]
        public int mana = 3;
        [Tooltip("Whether cards can be used or not. Still allows selection & reordering. e.g. Could be used to disable using cards when it's not the player's turn.")]
        public bool canUseCards = true;
        [Tooltip("Whether cards can interact with the mouse. Cards also cannot be used. (If a card is held, it will be returned to the hand)")]
        public bool canSelectCards = true;

        [Header("Settings")]

        [SerializeField]
        [Tooltip("Force cards to face upwards when selected, for easier readability.")]
        private bool cardUprightWhenSelected = true;
        [SerializeField]
        [Tooltip("Allow cards to tilt when not in hand, based on the velocity of mouse movement.")]
        private bool cardTilt = true;
        [SerializeField] [Range(0, 5)]
        [Tooltip("Controls the strength of the spacing given to cards nearby to the selected card.")]
        private float selectionSpacing = 1;
        
        //[SerializeField]
        //[Tooltip("Whether the order of cards in the hand should change the sibling order in Hierarchy (and parent when not in hand). " +
        //    "Useful if hierarchy order is important to determine render order... UI?")]   
        private bool updateHierarchyOrder = false;
        // While this works, there are various scaling issues with using this for UI.
        // I see no other use, so I've removed it from inspector

        [SerializeField]
        [Tooltip("Controls the curve that the hand uses.")]
        private Vector3 curveStart = new Vector3(2f, -0.7f, 0), curveEnd = new Vector3(-2f, -0.7f, 0);
        
        [SerializeField]
        [Tooltip("Controls the area which is considered 'in-hand', allowing cards to be selected/reordered. " +
            "If a card leaves this area it can be used upon releasing the mouse button. " +
            "Recommend having the hand bounds go past the screen edges to prevent accidental use when reordering cards quickly")]
        private Vector2 handOffset = new Vector2(0, -0.3f), handSize = new Vector2(9, 1.7f);
        
        [Header("References")]

        [SerializeField]
        [Tooltip("Overlay camera that is rendering the cards. Used for raycasting mouse position")]
        private Camera cam = null;
        [SerializeField]
        [Tooltip("Material to use when card cannot be used (e.g. not enough mana).")]
        private Material inactiveCardMaterial = null;

        private Plane plane; // world XY plane, used for mouse position raycasts
        private Vector3 a, b, c; // Used for shaping hand into curve
        
        private List<Card> hand; // Cards currently in hand
        
        private int selected = -1;  // Card index that is nearest to mouse
        private int dragged = -1;   // Card index that is held by mouse (inside of hand)
        private Card heldCard;      // Card that is held by mouse (when outside of hand)
        private Vector3 heldCardOffset;
        private Vector2 heldCardTilt;
        private Vector2 force;
        private Vector3 mouseWorldPos;
        private Vector2 prevMousePos;
        private Vector2 mousePosDelta;

        private Rect handBounds;
        private bool mouseInsideHand;

        private bool showDebugGizmos = true;

        private void Start() {
            a = transform.TransformPoint(curveStart);
            b = transform.position;
            c = transform.TransformPoint(curveEnd);
            handBounds = new Rect((handOffset - handSize / 2), handSize);
            plane = new Plane(-Vector3.forward, transform.position);
            prevMousePos = Input.mousePosition;

            // Add transform children to hand
            int count = transform.childCount;
            hand = new List<Card>(count);
            for (int i = 0; i < count; i++) {
                Transform cardTransform = transform.GetChild(i);
                Card card = cardTransform.GetComponent<Card>();
                hand.Add(card);
            }
        }

        private void OnDrawGizmos() {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.blue;
            
            Gizmos.DrawSphere(curveStart, 0.03f);
            //Gizmos.DrawSphere(Vector3.zero, 0.03f);
            Gizmos.DrawSphere(curveEnd, 0.03f);

            Vector3 p1 = curveStart;
            for (int i = 0; i < 20; i++) {
                float t = (i + 1) / 20f;
                Vector3 p2 = GetCurvePoint(curveStart, Vector3.zero, curveEnd, t);
                Gizmos.DrawLine(p1, p2);
                p1 = p2;
            }

            if (mouseInsideHand) {
                Gizmos.color = Color.red;
            }

            Gizmos.DrawWireCube(handOffset, handSize);
        }

        private void Update() {

            // --------------------------------------------------------
            // HANDLE MOUSE & RAYCAST POSITION
            // --------------------------------------------------------

            Vector2 mousePos = Input.mousePosition;

            // Allows mouse to go outside game window but keeps cards within window
            // If mouse doesn't need to go outside, could use "Cursor.lockState = CursorLockMode.Confined;" instead
            mousePos.x = Mathf.Clamp(mousePos.x, 0, Screen.width);
            mousePos.y = Mathf.Clamp(mousePos.y, 0, Screen.height);

            // Mouse movement velocity
            if (cardTilt) {
                mousePosDelta = (mousePos - prevMousePos) * new Vector2(1600f/Screen.width, 900f/Screen.height) * Time.deltaTime;
                prevMousePos = mousePos;
                
                float tiltStrength = 3f;
                float tiltDrag = 3f;
                float tiltSpeed = 50f;
                
                force += (mousePosDelta * tiltStrength - heldCardTilt) * Time.deltaTime;
                force *= 1 - tiltDrag * Time.deltaTime;
                heldCardTilt += force * Time.deltaTime * tiltSpeed;
                // these calculations probably aren't correct, but hey, they work...? :P

                if (showDebugGizmos) {
                    Debug.DrawRay(mouseWorldPos, mousePosDelta, Color.red);
                    Debug.DrawRay(mouseWorldPos, heldCardTilt, Color.cyan);
                }
            }

            // Get world position from mouse
            Ray ray = cam.ScreenPointToRay(mousePos);
            if (plane.Raycast(ray, out float enter)) {
                mouseWorldPos = ray.GetPoint(enter);
            }
            
            // Get distance to current selected card (for comparing against other cards later, to find closest)
            int count = hand.Count; //transform.childCount;
            float sqrDistance = 1000;
            if (selected >= 0 && selected < count) {
                float t = (selected + 0.5f) / count;
                Vector3 p = GetCurvePoint(a, b, c, t);
                sqrDistance = (p - mouseWorldPos).sqrMagnitude;
            }

            // Check if mouse is inside hand bounds
            Vector3 point = transform.InverseTransformPoint(mouseWorldPos);
            mouseInsideHand = handBounds.Contains(point);

            bool mouseButton = Input.GetMouseButton(0);

            // --------------------------------------------------------
            // HANDLE CARDS IN HAND
            // --------------------------------------------------------

            for (int i = 0; i < count; i++) {
                Card card = hand[i];
                Transform cardTransform = card.transform;

                // Set to inactive material if not enough mana required to use card
                card.SetInactiveMaterialState(mana < card.mana, inactiveCardMaterial);

                bool noCardHeld = (heldCard == null); // Whether a card is "held" (outside of hand)
                bool onSelectedCard = (noCardHeld && selected == i);
                bool onDraggedCard = (noCardHeld && dragged == i);
                
                // Get Position along Curve (for card positioning)
                float selectOffset = 0;
                if (noCardHeld) {
                    selectOffset = 0.02f * Mathf.Clamp01(1 - Mathf.Abs(Mathf.Abs(i - selected) - 1) / (float)count * 3) * Mathf.Sign(i - selected);
                }
                float t = (i + 0.5f) / count + selectOffset * selectionSpacing;
                Vector3 p = GetCurvePoint(a, b, c, t);

                float d = (p - mouseWorldPos).sqrMagnitude;
                bool mouseCloseToCard = d < 0.5f;
                bool mouseHoveringOnSelected = onSelectedCard && mouseCloseToCard && mouseInsideHand; //  && mouseInsideHand

                // Handle Card Position & Rotation
                //Vector3 cardUp = p - (transform.position + Vector3.down * 7);
                Vector3 cardUp = GetCurveNormal(a, b, c, t);
                Vector3 cardPos = p + (mouseHoveringOnSelected ? cardTransform.up * 0.3f : Vector3.zero);
                Vector3 cardForward = Vector3.forward;

                /* Card Tilt is disabled when in hand as they can clip through eachother :(
                if (cardTilt && onSelectedCard && mouseButton) {
                    cardForward -= new Vector3(heldCardOffset.x, heldCardOffset.y, 0);
                }*/

                // Sorting Order
                if (mouseHoveringOnSelected || onDraggedCard) {
                    // When selected bring card to front
                    if (cardUprightWhenSelected) cardUp = Vector3.up;
                    cardPos.z = transform.position.z - 0.2f;
                } else {
                    cardPos.z = transform.position.z + t * 0.5f;
                }

                // Rotation
                cardTransform.rotation = Quaternion.RotateTowards(cardTransform.rotation, Quaternion.LookRotation(cardForward, cardUp), 80f * Time.deltaTime);

                // Handle Start Dragging
                if (mouseHoveringOnSelected) {
                    bool mouseButtonDown = Input.GetMouseButtonDown(0);
                    if (mouseButtonDown) {
                        dragged = i;
                        heldCardOffset = cardTransform.position - mouseWorldPos;
                        heldCardOffset.z = -0.1f;
                    }
                }

                // Handle Card Position
                if (onDraggedCard && mouseButton) {
                    // Held by mouse / dragging
                    cardPos = mouseWorldPos + heldCardOffset;
                    cardTransform.position = cardPos;
                } else {
                    cardPos = Vector3.MoveTowards(cardTransform.position, cardPos, 6f * Time.deltaTime);
                    cardTransform.position = cardPos;
                }
                
                // Get Selected Card
                if (canSelectCards) {
                    //float d = (p - mouseWorldPos).sqrMagnitude;
                    if (d < sqrDistance) {
                        sqrDistance = d;
                        selected = i;
                    }
                } else {
                    selected = -1;
                    dragged = -1;
                }

                // Debug Gizmos
                if (showDebugGizmos) {
                    Color c = new Color(0, 0, 0, 0.2f);
                    if (i == selected) {
                        c = Color.red;
                        if (sqrDistance > 2) {
                            c = Color.blue;
                        }
                    }
                    Debug.DrawLine(p, mouseWorldPos, c);
                }
            }

            // --------------------------------------------------------
            // HANDLE DRAGGED CARD
            // (Card held by mouse, inside hand)
            // --------------------------------------------------------

            if (!mouseButton) {
                // Stop dragging
                heldCardOffset = Vector3.zero;
                dragged = -1;
            }

            if (dragged != -1) {
                Card card = hand[dragged];
                if (mouseButton && !mouseInsideHand) { //  && sqrDistance > 2.1f
                    //if (cardPos.y > transform.position.y + 0.5) {
                    // Card is outside of the hand, so is considered "held" ready to be used
                    // Remove from hand, so that cards in hand fill the hole that the card left
                    heldCard = card;
                    RemoveCardFromHand(dragged);
                    count--;
                    dragged = -1;
                }
            }

            if (heldCard == null && mouseButton && dragged != -1 && selected != -1 && dragged != selected) {
                // Move dragged card
                MoveCardToIndex(dragged, selected);
                dragged = selected;
            }

            // --------------------------------------------------------
            // HANDLE HELD CARD
            // (Card held by mouse, outside of the hand)
            // --------------------------------------------------------

            if (heldCard != null) {
                Transform cardTransform = heldCard.transform;
                Vector3 cardUp = Vector3.up;
                Vector3 cardPos = mouseWorldPos + heldCardOffset;
                Vector3 cardForward = Vector3.forward;
                if (cardTilt && mouseButton) {
                    cardForward -= new Vector3(heldCardTilt.x, heldCardTilt.y, 0);
                }

                // Bring card to front
                cardPos.z = transform.position.z - 0.2f;

                // Handle Position & Rotation
                cardTransform.rotation = Quaternion.RotateTowards(cardTransform.rotation, Quaternion.LookRotation(cardForward, cardUp), 80f * Time.deltaTime);
                cardTransform.position = cardPos;

                //if (!canSelectCards || cardTransform.position.y <= transform.position.y + 0.5f) {
                if (!canSelectCards || mouseInsideHand) { //  || sqrDistance <= 2
                    // Card has gone back into hand
                    AddCardToHand(heldCard, selected);
                    dragged = selected;
                    selected = -1;
                    heldCard = null;
                    return;
                }

                // Use Card
                bool mouseButtonUp = Input.GetMouseButtonUp(0);
                if (mouseButtonUp) {
                    if (canUseCards && mana >= heldCard.mana) {
                        mana -= heldCard.mana;
                        heldCard.Use();
                    } else {
                        // Cannot use card / Not enough mana! Return card to hand!
                        AddCardToHand(heldCard, selected);
                    }
                    heldCard = null;
                }
            }
        }

        /// <summary>
        /// Obtains a point along a curve based on 3 points. Equal to Lerp(Lerp(a, b, t), Lerp(b, c, t), t).
        /// </summary>
        public static Vector3 GetCurvePoint(Vector3 a, Vector3 b, Vector3 c, float t) {
            t = Mathf.Clamp01(t);
            float oneMinusT = 1f - t;
            return (oneMinusT * oneMinusT * a) + (2f * oneMinusT * t * b) + (t * t * c);
        }

        /// <summary>
        /// Obtains the derivative of the curve (tangent)
        /// </summary>
        public static Vector3 GetCurveTangent(Vector3 a, Vector3 b, Vector3 c, float t) {
            return 2f * (1f - t) * (b - a) + 2f * t * (c - b);
        }

        /// <summary>
        /// Obtains a direction perpendicular to the tangent of the curve
        /// </summary>
        public static Vector3 GetCurveNormal(Vector3 a, Vector3 b, Vector3 c, float t) {
            Vector3 tangent = GetCurveTangent(a, b, c, t);
            return Vector3.Cross(tangent, Vector3.forward);
        }

        /// <summary>
        /// Moves the card in hand from the currentIndex to the toIndex. If you want to move a card that isn't in hand, use AddCardToHand
        /// </summary>
        public void MoveCardToIndex(int currentIndex, int toIndex) {
            if (currentIndex == toIndex) return; // Same index, do nothing
            Card card = hand[currentIndex];
            hand.RemoveAt(currentIndex);
            hand.Insert(toIndex, card);

            if (updateHierarchyOrder) {
                card.transform.SetSiblingIndex(toIndex);
            }
        }

        /// <summary>
        /// Adds a card to the hand. Optional param to insert it at a given index.
        /// </summary>
        public void AddCardToHand(Card card, int index = -1) {
            if (index < 0) {
                // Add to end
                hand.Add(card);
                index = hand.Count - 1;
            } else {
                // Insert at index
                hand.Insert(index, card);
            }
            if (updateHierarchyOrder) {
                card.transform.SetParent(transform);
                card.transform.SetSiblingIndex(index);
            }
        }

        /// <summary>
        /// Remove the card at the specified index from the hand.
        /// </summary>
        public void RemoveCardFromHand(int index) {
            if (updateHierarchyOrder) {
                Card card = hand[index];
                card.transform.SetParent(transform.parent);
                card.transform.SetSiblingIndex(transform.GetSiblingIndex() + 1);
            }
            hand.RemoveAt(index);
        }
    }

}