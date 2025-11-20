using UnityEngine;
using UnityEngine.UI;
using System.Text;
using System.Collections;
using System.Collections.Generic;

public class LSystem : MonoBehaviour
{
    struct TransformInfo 
    {
        public Vector3 position;
        public Quaternion rotation;
    }

    private string currentString = string.Empty;
    public string axiom = "X";
    public int iterations = 3;
    public float angle = 30.0f;
    public float length = 10.0f;
    public float width = 0.01f;


    [SerializeField] private Dictionary<char, string> rules;
    private Stack<TransformInfo> transformStack;

    [SerializeField] private GameObject treeParent;
    [SerializeField] private GameObject branch;
    private GameObject Tree = null;
    private GameObject treeSegment;

    private void Start()
    {
        transformStack = new Stack<TransformInfo>();
        rules = new Dictionary<char, string> 
        {
            {'X', "[FX][-FX][+FX]"},
            {'F', "FF"}
        };

        transform.Rotate(Vector3.right * -90.0f);
        currentString = axiom;
        Generate();
    }

    private void Update()
    {
        
    }

    private void Generate() 
    {
        Destroy(Tree);
        Tree = Instantiate(treeParent);

        StringBuilder sb = new StringBuilder();
        LineRenderer lineRenderer = GetComponent<LineRenderer>();

        for (int i = 0; i < iterations; i++) 
        {
            foreach (char c in currentString) 
            {
                sb.Append(rules.ContainsKey(c) ? rules[c] : c.ToString());
            }

            currentString = sb.ToString();
            sb = new StringBuilder();
        }

        foreach (char c in currentString) 
        {
            switch (c)
            {
                case 'F':
                    Vector3 initialPosition = transform.position;
                    transform.Translate(Vector3.forward * length);
                    treeSegment = Instantiate(branch);
                    treeSegment.GetComponent<LineRenderer>().SetPosition(0, initialPosition);
                    treeSegment.GetComponent<LineRenderer>().SetPosition(1, transform.position);
                    treeSegment.GetComponent<LineRenderer>().startWidth = width;
                    treeSegment.GetComponent<LineRenderer>().endWidth = width;
                    treeSegment.transform.SetParent(Tree.transform);
                    break;
                case 'X':
                    break;
                case '+':
                    transform.Rotate(Vector3.up * angle);
                    break;
                case '-':
                    transform.Rotate(Vector3.up * -angle);
                    break;
                case '[':
                    transformStack.Push(new TransformInfo()
                    {
                        position = transform.position,
                        rotation = transform.rotation,
                    });
                    break;
                case ']':
                    {
                        TransformInfo ti = transformStack.Pop();
                        transform.position = ti.position;
                        transform.rotation = ti.rotation;
                    }
                    break;
                default:
                    Debug.Log("sadasdsa");
                    break;
            }

            length /= 2;
            width /= 2;
            Debug.Log(currentString);
            Debug.Log("Generate");
        }
    }
}
