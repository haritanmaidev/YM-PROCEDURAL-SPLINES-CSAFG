using UnityEngine;

public class LSystems : MonoBehaviour
{
    private string tree;
    public string axiom;
    public int iterations;

    private void Start()
    {
        tree = axiom;
        Debug.Log("Starting tree: " + tree);
        ExpandTreeString();
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


}
