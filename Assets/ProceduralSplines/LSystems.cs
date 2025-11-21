using System.Collections.Generic;
using UnityEngine;

public class LSystems : MonoBehaviour
{
    public LSystemSO SO;
    private string tree;
    public string axiom;
    public string ruleF = "FF";
    public string ruleB = "[+FB][-FB]";
    [Range(1, 10)]public int iterations;
    [Range(0, 180)] public float angle;
    [Range(0.001f, 1f)] public float length;
    public bool enableRandomness = false;
    private List<List<Vector3>> LineList = new List<List<Vector3>> ();
    Stack<LSystemTransformInfo> stack = new Stack<LSystemTransformInfo> ();

    private void Start()
    {
        if (SO != null)
        {
            axiom = SO.axiom;
            iterations = SO.iterations;
            length = SO.length;
            angle = SO.angle;
            enableRandomness = SO.enableRandomness;

            ruleF = SO.ruleF;
            ruleB = SO.ruleB;
        }

        tree = axiom;
        Debug.Log("Starting tree: " + tree);
        ExpandTreeString();
        CreateMesh();
    }

    void ExpandTreeString() 
    {
        string expandedTree;

        //Loop through each cahr of the string and replace it based on rules
        for (int i = 0; i < iterations; i++) 
        {
            expandedTree = "";

            //Assign the new string after each iteration to tree
            foreach (char j in tree) 
            {
                switch (j) 
                {
                    case 'F':
                        if (enableRandomness && Random.Range(0f, 100f) < 50f)
                        {
                            expandedTree += "F";
                        }
                        else 
                        {
                            expandedTree += ruleF;
                        }
                        break;
                    case 'B':
                        float random = Random.Range(0f, 100f);
                        if (enableRandomness && random < 50f)
                        {
                            //Choose which side to rotate an extra amount
                            expandedTree += (random < 25f) ? "[++FB][-FB]" : "[+FB][--FB]";
                        }
                        else 
                        {
                            expandedTree += ruleB;
                        }
                        break;
                    default:
                        expandedTree += j.ToString();
                        break;
                }
            }

            tree = expandedTree;
            Debug.Log("Tree at iteration "+i+" is "+tree);
        }
    }

    void CreateMesh() 
    {
        Vector3 initialPoisition;

        //Loops through final tree string and define instruction based on the char
        foreach (char j in tree) 
        {
            switch (j) 
            {
                case 'F': //Move forward
                    initialPoisition = transform.position;
                    transform.Translate(Vector3.up * length);
                    LineList.Add(new List<Vector3>() { initialPoisition, transform.position}); //Add start and end points of the line
                    initialPoisition = transform.position; //Setting initial pos for next iteration
                    break;
                case 'B':
                    //Does nothing
                    break;
                case '[': //Push current drawing state onto stack
                    stack.Push(new LSystemTransformInfo()
                    {
                        position = transform.position,
                        rotation = transform.rotation,
                    });
                    break;
                case ']': //Pop current state from from stack
                    LSystemTransformInfo helper = stack.Pop();
                    transform.position = helper.position;
                    transform.rotation = helper.rotation;
                    break;
                case '+': //Rotate left
                    transform.Rotate(Vector3.back, angle);
                    break;
                case '-': //Rotate right
                    transform.Rotate(Vector3.forward, angle);
                    break;
            }
        }
    }

    private void OnDrawGizmos()
    {
        foreach (List<Vector3> line in LineList)
        {
            Gizmos.DrawLine(line[0], line[1]);
        }
    }
}
