using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using System.Text.RegularExpressions;
using rnd = UnityEngine.Random;

public class ro : MonoBehaviour
{
    public new KMAudio audio;
    public KMBombInfo bomb;
    public KMBombModule module;

    public KMSelectable[] buttons;
    public Transform[] diamonds;
    public Color[] diamondColors;

    private int[] currentState;
    private int[] desiredState;

    private static readonly int[][] adjacentIndices = new int[][]
    {
        new int[] { 1, 5, 6, 2 },
        new int[] { 2, 6, 7, 3 },
        new int[] { 3, 7, 8, 4 },
        new int[] { 5, 9, 10, 6 },
        new int[] { 6, 10, 11, 7 },
        new int[] { 7, 11, 12, 8 },
        new int[] { 9, 13, 14, 10 },
        new int[] { 10, 14, 15, 11 },
        new int[] { 11, 15, 16, 12 }
    };
    private readonly Queue<int> animationQueue = new Queue<int>();

    private bool active;
    private static int moduleIdCounter = 1;
    private int moduleId;
    private bool moduleSolved;

    private void Awake()
    {
        moduleId = moduleIdCounter++;
        module.OnActivate += delegate () { active = true; StartCoroutine(Animate()); };
        foreach (KMSelectable button in buttons)
        {
            var ix = Array.IndexOf(buttons, button);
            button.OnInteract += delegate ()
            {
                if (!moduleSolved && active)
                {
                    button.AddInteractionPunch(.25f);
                    audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, button.transform);
                    Swap(ix, true);
                }
                return false;
            };
        }
        desiredState = Enumerable.Range(1, 16).ToArray();
        currentState = desiredState.ToArray();
        for (int i = 0; i < 16; i++)
        {
            diamonds[i].GetComponent<Renderer>().material.color = diamondColors[i];
        }
        for (int i = 0; i < 100; i++)
            Swap(rnd.Range(0, 9));
        for (int i = 0; i < 16; i++)
        {
            diamonds[i].GetComponent<Renderer>().material.color = diamondColors[currentState[i] - 1];
        };
        Debug.LogFormat("[Ro #{0}] Starting state: {1}", moduleId, currentState.Join(", "));
    }

    private void Swap(int ix, bool animate = false)
    {
        var ix1 = adjacentIndices[ix][0] - 1;
        var ix2 = adjacentIndices[ix][1] - 1;
        var ix3 = adjacentIndices[ix][2] - 1;
        var ix4 = adjacentIndices[ix][3] - 1;
        var s1 = currentState[ix1];
        var s2 = currentState[ix2];
        var s3 = currentState[ix3];
        var s4 = currentState[ix4];
        currentState[ix1] = s4;
        currentState[ix2] = s1;
        currentState[ix3] = s2;
        currentState[ix4] = s3;
        if (animate)
            animationQueue.Enqueue(ix);
    }

    private IEnumerator Animate()
    {
        while (true)
        {
            while (animationQueue.Count == 0)
                yield return null;
            var ix = animationQueue.Dequeue();
            var elapsed = 0f;
            var duration = .25f;
            var diamondsToMove = adjacentIndices[ix].Select(x => diamonds[x - 1]).ToArray();
            var startingPositions = new float[4];
            for (int i = 0; i < 4; i++)
                startingPositions[i] = i % 2 == 0 ? diamondsToMove[i].localPosition.z : diamondsToMove[i].localPosition.x;
            while (elapsed < duration)
            {
                diamondsToMove[0].localPosition = new Vector3(diamondsToMove[0].localPosition.x, diamondsToMove[0].localPosition.y, Easing.OutSine(elapsed, startingPositions[0], startingPositions[0] - .036f, duration));
                diamondsToMove[1].localPosition = new Vector3(Easing.OutSine(elapsed, startingPositions[1], startingPositions[1] + .036f, duration), diamondsToMove[1].localPosition.y, diamondsToMove[1].localPosition.z);
                diamondsToMove[2].localPosition = new Vector3(diamondsToMove[2].localPosition.x, diamondsToMove[2].localPosition.y, Easing.OutSine(elapsed, startingPositions[2], startingPositions[2] + .036f, duration));
                diamondsToMove[3].localPosition = new Vector3(Easing.OutSine(elapsed, startingPositions[3], startingPositions[3] - .036f, duration), diamondsToMove[3].localPosition.y, diamondsToMove[3].localPosition.z);
                yield return null;
                elapsed += Time.deltaTime;
            }
            diamondsToMove[0].localPosition = new Vector3(diamondsToMove[0].localPosition.x, diamondsToMove[0].localPosition.y, startingPositions[0] - .036f);
            diamondsToMove[1].localPosition = new Vector3(startingPositions[1] + .036f, diamondsToMove[1].localPosition.y, diamondsToMove[1].localPosition.z);
            diamondsToMove[2].localPosition = new Vector3(diamondsToMove[2].localPosition.x, diamondsToMove[2].localPosition.y, startingPositions[2] + .036f);
            diamondsToMove[3].localPosition = new Vector3(startingPositions[3] - .036f, diamondsToMove[3].localPosition.y, diamondsToMove[3].localPosition.z);
            var ix1 = adjacentIndices[ix][0] - 1;
            var ix2 = adjacentIndices[ix][1] - 1;
            var ix3 = adjacentIndices[ix][2] - 1;
            var ix4 = adjacentIndices[ix][3] - 1;
            var gs1 = diamonds[ix1];
            var gs2 = diamonds[ix2];
            var gs3 = diamonds[ix3];
            var gs4 = diamonds[ix4];
            diamonds[ix1] = gs4;
            diamonds[ix2] = gs1;
            diamonds[ix3] = gs2;
            diamonds[ix4] = gs3;
            if (!moduleSolved && currentState.SequenceEqual(desiredState))
            {
                moduleSolved = true;
                module.HandlePass();
                Debug.LogFormat("[Ro #{0}] Module solved!", moduleId);
                audio.PlaySoundAtTransform("solve", transform);
            }
        }
    }

    // Twitch Plays
#pragma warning disable 414
    private readonly string TwitchHelpMessage = "!{0} <TL/TM/TR/ML/MM/MR/BL/BM/BR> [Presses the buttons in those positions. Can be chained.]";
#pragma warning restore 414

    private IEnumerator ProcessTwitchCommand(string input)
    {
        input = input.Trim().ToUpperInvariant();
        var inputArray = input.Split(' ').ToArray();
        var validInputs = new string[] { "TL", "TM", "TR", "ML", "MM", "MR", "BL", "BM", "BR" };
        if (inputArray.Any(x => !validInputs.Contains(x)))
            yield break;
        yield return null;
        foreach (string str in inputArray)
        {
            yield return new WaitForSeconds(.1f);
            buttons[Array.IndexOf(validInputs, str)].OnInteract();
        }
    }
}
