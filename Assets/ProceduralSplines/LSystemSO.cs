using UnityEngine;

[CreateAssetMenu(fileName = "LSystemPreset", menuName = "Scriptable Objects/LSystemPreset")]
public class LSystemSO : ScriptableObject
{
    public string axiom = "FB";
    public string ruleF = "F[+F]F[-F]F";
    public string ruleB = "[+FB][-FB]";
    [Range(0, 10)] public int iterations = 3;
    [Range(0f, 180f)] public float angle = 25f;
    [Range(0.01f, 1f)] public float length = 0.25f;
    public bool enableRandomness = false;
}
