using System.Collections;
using UnityEngine;
using TMPro;

namespace NumbersBlast.UI
{
    /// <summary>
    /// Fake "finding opponent" overlay shown before a Vs AI match starts (and again on replay) —
    /// sells the case brief's "fake real-time multiplayer" illusion. Purely cosmetic; no networking
    /// involved, the opponent is local and scripted.
    /// </summary>
    public sealed class MatchmakingOverlay : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private float searchingSecondsMin = 2f;
        [SerializeField] private float searchingSecondsMax = 2.8f;
        [SerializeField] private float foundHoldSeconds = 0.5f;

        private const string Searching = "Finding opponent";
        private const string Found = "Opponent found!";

        public IEnumerator PlayConnecting()
        {
            if (root != null)
            {
                root.SetActive(true);
            }

            // Randomized per attempt so the fake wait doesn't feel scripted/identical every time.
            float searchingSeconds = Random.Range(searchingSecondsMin, searchingSecondsMax);
            float t = 0f;
            while (t < searchingSeconds)
            {
                t += Time.deltaTime;
                int dots = ((int)(t * 2f) % 4);
                if (statusText != null)
                {
                    statusText.text = Searching + new string('.', dots);
                }

                yield return null;
            }

            if (statusText != null)
            {
                statusText.text = Found;
            }

            yield return new WaitForSeconds(foundHoldSeconds);

            if (root != null)
            {
                root.SetActive(false);
            }
        }
    }
}
