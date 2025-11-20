using System.Collections.Generic;
using UnityEngine;

public class LSystems : MonoBehaviour
{
    private string tree;
    public string axiom;
    [Range(0, 10)]public int iterations;
    Stack<LSystemTransformInfo> stack = new Stack<LSystemTransformInfo> ();
    LSystemTransformInfo helper;
    public float length;
    private List<List<Vector3>> LineList = new List<List<Vector3>> ();
    [Range(0, 100)] public float angle;

    private void Start()
    {
        tree = axiom;
        Debug.Log("Starting tree: " + tree);
        ExpandTreeString();
        CreateMesh();
    }

    private void OnDrawGizmos()
    {
        foreach(List<Vector3> line in LineList) 
        {
            Gizmos.DrawLine(line[0], line[1]);
        }
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
                expandedTree += j switch
                {
                    'F' => "FF",
                    'B' => "[lFB] [rFB]",
                    _ => j.ToString()
                };
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
                case 'F':
                    initialPoisition = transform.position;
                    transform.Translate(Vector3.up * length);
                    LineList.Add(new List<Vector3>() { initialPoisition, transform.position}); //Add start and end points of the line
                    initialPoisition = transform.position; //Setting initial pos for next iteration
                    break;

                case 'B':
                    //Does nothing
                    break;

                case '[':
                    stack.Push(new LSystemTransformInfo()
                    {
                        position = transform.position,
                        rotation = transform.rotation,
                    });
                    break;

                case ']':
                    LSystemTransformInfo helper = stack.Pop(); //Set current pos and rot from popped stack
                    transform.position = helper.position;
                    transform.rotation = helper.rotation;
                    break;

                case 'l':
                    transform.Rotate(Vector3.back, angle);
                    break;

                case 'r':
                    transform.Rotate(Vector3.forward, angle);

                    break;
            }
        }
    }
}
