using System.Collections.Generic;
using UnityEngine;
using Febucci.UI;
using UnityEngine.UI;
using TMPro;

public class Cell
{
    public Vector2Int ID { get; set; }
    public int RealValue { get; set; }
    public string ShownValue { get; set; }
    public List<int> PossibleValues { get; set; }
    public string PossibleValuesString
    {
        get
        {
            string str = "";
            foreach (int val in PossibleValues)
                str += val + " ";
            return str;
        }
    }
    public TextAnimator TextAnimator { get; private set; }
    public TextMeshProUGUI DebugText { get; private set; }
    public TextMeshProUGUI DebugPossibilitiesText { get; private set; }
    public Button Button { get; private set; }
    public bool Visible { get { return ShownValue != ""; } }
    public bool Locked { get { return !Button.interactable; } }
    public bool Correct { get { return RealValue.ToString() == ShownValue; } }


    public Cell(Vector2Int id, TextAnimator textAnimator, TextMeshProUGUI debugText, TextMeshProUGUI debugPossibilitiesText)
    {
        ID = id;
        RealValue = -1;
        TextAnimator = textAnimator;
        DebugText = debugText;
        DebugPossibilitiesText = debugPossibilitiesText;
        Button = TextAnimator.GetComponentInParent<Button>();
        PossibleValues = new List<int>(0);
        ShownValue = RealValue == -1 ? "" : RealValue.ToString();
    }

    public Cell(int realValue, Vector2Int id, TextAnimator textAnimator, TextMeshProUGUI debugText, TextMeshProUGUI debugPossibilitiesText)
    {
        ID = id;
        RealValue = realValue;
        TextAnimator = textAnimator;
        DebugText = debugText;
        DebugPossibilitiesText = debugPossibilitiesText;
        Button = TextAnimator.GetComponentInParent<Button>();
        PossibleValues = new List<int>(0);
        ShownValue = RealValue == -1 ? "" : RealValue.ToString();
    }

    public void SetUIText(string text)
    {
        ShownValue = text;
        TextAnimator.SetText(text, false);
    }

    public void SetDebugUIText(string text) => DebugText.SetText(text);

    public void SetDebugPossibilitiesUIText(string text) => DebugPossibilitiesText.SetText(text);

    public void Reset()
    {
        RealValue = -1;
        SetUIText("");
        SetDebugUIText("");
        SetDebugPossibilitiesUIText("");
    }
}
