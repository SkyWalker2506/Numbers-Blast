using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using NumbersBlast.Core;
using NumbersBlast.Data;

namespace NumbersBlast.Presentation
{
    /// <summary>
    /// Plays the juice for a resolved move: placement pops, an animated merge (the absorbed blocks
    /// scale up and fly into the fixed merge target, then it reveals the summed value with a punch),
    /// line-clear flash, floating score, and the game-over board fade wave. Reads a MoveResult.
    /// </summary>
    public sealed class GameplayAnimationController : MonoBehaviour
    {
        [SerializeField] private RectTransform floatingScoreLayer;
        [SerializeField] private TMP_Text floatingScorePrefab;
        [SerializeField] private Sprite glowSprite;
        [SerializeField] private BoardConfig boardConfig;
        [Tooltip("Seconds for one merge step's fly-in (snappy; chains play these back to back).")]
        [SerializeField] private float mergeEffectTime = 0.09f;
        [SerializeField] private float lineClearFlashTime = 0.28f;
        [SerializeField] private float cellFadeTime = 0.18f;
        [SerializeField] private float gameOverWaveStep = 0.02f;

        private readonly Stack<GameObject> _glowPool = new Stack<GameObject>();

        public IEnumerator PlayMove(MoveResult result, BoardView board)
        {
            if (result == null || !result.IsValid)
            {
                yield break;
            }

            // Rebuild the *pre-merge* board so nothing pops out of existence: every block that takes
            // part in any merge step (targets and the neighbours that will fly in later) is shown at
            // its original value up front. The board was refreshed to the final state before this, so
            // without this a later step's source cell would be blank until its own step animates.
            if (result.HasMerge)
            {
                var seenTargets = new HashSet<Vector2Int>();
                foreach (MergeStep step in result.MergeSteps)
                {
                    if (seenTargets.Add(step.Target))
                    {
                        board.GetCell(step.Target)?.SetValue(step.OriginalValue, ColorFor(step.OriginalValue));
                    }

                    foreach (Vector2Int src in step.SourceCells)
                    {
                        if (src != step.Target)
                        {
                            board.GetCell(src)?.SetValue(step.OriginalValue, ColorFor(step.OriginalValue));
                        }
                    }
                }
            }

            foreach (Vector2Int pos in result.PlacedCells)
            {
                CellView cell = board.GetCell(pos);
                if (cell != null)
                {
                    cell.PlayPop();
                }
            }

            // Chain reactions play one step at a time, each with the same fly-in effect.
            foreach (MergeStep step in result.MergeSteps)
            {
                yield return PlayMergeStep(step, board);
            }

            if (result.HasLineClear)
            {
                foreach (Vector2Int pos in result.ClearedCells)
                {
                    CellView cell = board.GetCell(pos);
                    if (cell != null)
                    {
                        cell.SetHighlight(CellHighlightType.LineClear);
                        SpawnGlow(board.GetCellWorldCenter(pos), 130f, new Color(0.6f, 0.85f, 1f), 0.45f);
                    }
                }

                if (result.ScoreGain > 0)
                {
                    ShowFloatingScore(result.ScoreGain, CenterOf(result.ClearedCells, board));
                }

                yield return new WaitForSeconds(lineClearFlashTime);

                foreach (Vector2Int pos in result.ClearedCells)
                {
                    CellView cell = board.GetCell(pos);
                    if (cell != null)
                    {
                        cell.ClearHighlight();
                        cell.SetEmpty();   // sync visuals to the cleared model (merge may have left a value here)
                    }
                }
            }
            else if (!result.HasMerge)
            {
                yield return new WaitForSeconds(0.08f);
            }
        }

        /// <summary>
        /// Animates one merge: the merge target stays fixed while every other block in the group scales
        /// up, then flies to the target while returning to normal size — all arriving together — after
        /// which the target reveals the summed value with a punch and a glow.
        /// </summary>
        private IEnumerator PlayMergeStep(MergeStep step, BoardView board)
        {
            CellView target = board.GetCell(step.Target);
            if (target == null)
            {
                yield break;
            }

            Vector3 targetPos = board.GetCellWorldCenter(step.Target);
            float size = target.RectTransform.rect.width;
            Sprite sprite = target.FilledSprite;

            // Keep the target on its pre-merge value while the neighbours travel in.
            target.SetValue(step.OriginalValue, ColorFor(step.OriginalValue));

            var ghosts = new List<RectTransform>();
            var starts = new List<Vector3>();
            foreach (Vector2Int src in step.SourceCells)
            {
                if (src == step.Target)
                {
                    continue;
                }

                // Lift the block off the board into the flying ghost (so there's no duplicate beneath).
                board.GetCell(src)?.SetEmpty();
                GameObject g = SpawnGhostBlock(board.GetCellWorldCenter(src), size, step.OriginalValue, sprite);
                ghosts.Add((RectTransform)g.transform);
                starts.Add(g.transform.position);
            }

            float t = 0f;
            while (t < mergeEffectTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / mergeEffectTime);
                // Scale: pop up in the first third, then settle toward normal.
                float scale = k < 0.35f
                    ? Mathf.Lerp(1f, 1.25f, k / 0.35f)
                    : Mathf.Lerp(1.25f, 0.95f, (k - 0.35f) / 0.65f);
                // Move: hold briefly (the pop), then ease into the target so all ghosts arrive together.
                float moveK = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - 0.25f) / 0.75f));

                for (int i = 0; i < ghosts.Count; i++)
                {
                    ghosts[i].position = Vector3.Lerp(starts[i], targetPos, moveK);
                    ghosts[i].localScale = Vector3.one * scale;
                }

                yield return null;
            }

            foreach (RectTransform g in ghosts)
            {
                Destroy(g.gameObject);
            }

            // Reveal the merged value.
            target.SetValue(step.ResultValue, ColorFor(step.ResultValue));
            target.PlayPunch();
            SpawnGlow(targetPos, size * 1.7f, new Color(1f, 0.95f, 0.6f), 0.4f);
        }

        private GameObject SpawnGhostBlock(Vector3 worldPosition, float size, int value, Sprite sprite)
        {
            var go = new GameObject("MergeGhost", typeof(RectTransform));
            go.transform.SetParent(floatingScoreLayer, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(size, size);
            rt.position = worldPosition;

            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.color = ColorFor(value);
            img.raycastTarget = false;

            if (floatingScorePrefab != null)
            {
                var txtGo = new GameObject("Value", typeof(RectTransform));
                txtGo.transform.SetParent(rt, false);
                var trt = (RectTransform)txtGo.transform;
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = Vector2.zero;
                trt.offsetMax = Vector2.zero;

                var t = txtGo.AddComponent<TextMeshProUGUI>();
                t.font = floatingScorePrefab.font;
                t.text = value.ToString();
                t.fontSize = size * 0.5f;
                t.alignment = TextAlignmentOptions.Center;
                t.fontStyle = FontStyles.Bold;
                t.color = Color.white;
                t.raycastTarget = false;
            }

            return go;
        }

        private Color ColorFor(int value)
        {
            return boardConfig != null ? boardConfig.GetColorForValue(value) : Color.white;
        }

        public IEnumerator PlayGameOverWave(BoardView board, int width, int height)
        {
            for (int y = height - 1; y >= 0; y--)
            {
                for (int x = 0; x < width; x++)
                {
                    CellView cell = board.GetCell(new Vector2Int(x, y));
                    if (cell != null)
                    {
                        StartCoroutine(cell.FadeOut(cellFadeTime));
                    }
                }

                yield return new WaitForSeconds(gameOverWaveStep);
            }

            yield return new WaitForSeconds(cellFadeTime);
        }

        public void ShowFloatingScore(int amount, Vector3 worldPosition)
        {
            if (floatingScorePrefab == null || floatingScoreLayer == null)
            {
                return;
            }

            TMP_Text text = Instantiate(floatingScorePrefab, floatingScoreLayer);
            text.text = "+" + amount;
            text.transform.position = worldPosition;
            StartCoroutine(FloatAndFade(text));
        }

        private IEnumerator FloatAndFade(TMP_Text text)
        {
            const float duration = 1.0f;
            Vector3 start = text.transform.position;
            Vector3 end = start + new Vector3(0f, 110f, 0f);
            Color color = text.color;
            Transform tr = text.transform;
            float t = 0f;

            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                tr.position = Vector3.Lerp(start, end, 1f - (1f - k) * (1f - k)); // ease-out
                float scale = k < 0.2f ? Mathf.Lerp(0.5f, 1.15f, k / 0.2f) : Mathf.Lerp(1.15f, 1f, (k - 0.2f) / 0.8f);
                tr.localScale = Vector3.one * scale;
                float alpha = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
                text.color = new Color(color.r, color.g, color.b, alpha);
                yield return null;
            }

            Destroy(text.gameObject);
        }

        private void SpawnGlow(Vector3 worldPosition, float size, Color tint, float duration)
        {
            if (glowSprite == null || floatingScoreLayer == null)
            {
                return;
            }

            GameObject go = GetGlow();
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(size, size);
            rt.position = worldPosition;

            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.sprite = glowSprite;
            img.color = new Color(tint.r, tint.g, tint.b, 0.9f);

            StartCoroutine(GlowRoutine(go, img, rt, duration));
        }

        private GameObject GetGlow()
        {
            GameObject go;
            if (_glowPool.Count > 0)
            {
                go = _glowPool.Pop();
                go.SetActive(true);
            }
            else
            {
                go = new GameObject("Glow", typeof(RectTransform));
                go.transform.SetParent(floatingScoreLayer, false);
                var img = go.AddComponent<UnityEngine.UI.Image>();
                img.raycastTarget = false;
            }

            return go;
        }

        private IEnumerator GlowRoutine(GameObject go, UnityEngine.UI.Image img, RectTransform rt, float duration)
        {
            Color baseColor = img.color;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                rt.localScale = Vector3.Lerp(Vector3.one * 0.5f, Vector3.one * 1.7f, k);
                img.color = new Color(baseColor.r, baseColor.g, baseColor.b, (1f - k) * 0.9f);
                yield return null;
            }

            go.SetActive(false);
            _glowPool.Push(go);
        }

        private static Vector3 CenterOf(System.Collections.Generic.IReadOnlyList<Vector2Int> cells, BoardView board)
        {
            if (cells.Count == 0)
            {
                return board.GetCellWorldCenter(Vector2Int.zero);
            }

            Vector3 sum = Vector3.zero;
            foreach (Vector2Int pos in cells)
            {
                sum += board.GetCellWorldCenter(pos);
            }

            return sum / cells.Count;
        }
    }
}
