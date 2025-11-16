using UnityEngine;

// This script will control a LineRenderer and
// later generate spline points for it.
[RequireComponent(typeof(LineRenderer))]
public class PGSSpline : MonoBehaviour
{
    [Header("Manual Mode")]
    public Transform[] controlPoints;

    [Header("Procedural Mode")]
    public Transform startPoint;
    public Transform endPoint;

    [Range(-5f, 5f)] public float curvature = 0f;
    [Range(2, 30)] public int splinePointCount = 4;
    [Range(1, 10)] public int segmentResolution = 1;

    public bool useProceduralPoints = true;
    public Vector3[] generatedPoints;

    private LineRenderer line;

//CHECK: is splinePointCount same as interalPointCount?
//TODO: Curvature now can determine only left and right curve arching. it should be able to go up and down as well
//TODO: Arc factor weights the curvature whose weight at A and B = 0, mid point = 1. Spreads the effect of curvature along the spline
//Maybe expose where the peak weight should be, for more control


    private void Awake()
    {
        line = GetComponent<LineRenderer>();
    }

    private void Update()
    {
        if (useProceduralPoints)
        {
            GenerateControlPointsFromAtoB();

            if (generatedPoints != null && generatedPoints.Length >= 4) DrawCatmullRomProceduralSpline(generatedPoints);
        }
        else
        {
            if (controlPoints != null && controlPoints.Length >= 4) DrawCatmullRomFromTransforms(controlPoints);
        }

    }

    private Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        // Calculate the Catmull-Rom spline point at parameter t
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * ((2f * p1) +
                       (-p0 + p2) * t +
                       (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                       (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private void GenerateControlPointsFromAtoB()
    {
        if (startPoint == null || endPoint == null)
            return;

        Vector3 A = startPoint.position;
        Vector3 B = endPoint.position;
        Vector3 AB = B - A;

        float distance = AB.magnitude;
        Vector3 dir = AB.normalized;

        //Build array of points: A/Start + internal/spline points + B/End
        int totalPoints = splinePointCount + 2;
        generatedPoints = new Vector3[totalPoints];
        generatedPoints[0] = A;
        generatedPoints[totalPoints - 1] = B;

        #region 4 manual point old code
            // Two base points along the straight line A -> B
            //Vector3 p1Base = A;
            //Vector3 p2Base = A + dir * (distance * 1.0f);
            //Vector3 p1Base = A + dir * (distance * 0.33f); // about 1/3 of the way
            //Vector3 p2Base = A + dir * (distance * 0.66f); // about 2/3 of the way
        #endregion

        // Sideways direction (perpendicular to AB, using world up)
        Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;

        //Looping through p1 to pN-2: Only the spline points and not the start/end points
        for (int p = 1; p <= totalPoints - 2; p++) 
        {
            float t = p / (float)(totalPoints - 1);
            Vector3 onLine = A + dir * (distance * t);

            // Curvature offset; you can scale by distance if you want stronger bend on long lines
            float arcFactor = Mathf.Sin(Mathf.PI * t);
            Vector3 offset = side * curvature * arcFactor;

            generatedPoints[p] = onLine + offset;
        }
    }

    private void DrawStraightLinesBetweenControlPoints()
    {
        // Safety checks: if no control points or only one, we can't draw a line.
        if (controlPoints == null || controlPoints.Length < 2) return;

        // The line needs as many positions as we have control points.
        line.positionCount = controlPoints.Length;

        // Loop through all control points and set their positions in the line.
        for (int i = 0; i < controlPoints.Length; i++)
        {
            line.SetPosition(i, controlPoints[i].position);
        }
    }

    private void DrawCatmullRomSpline() 
    {
        if (controlPoints == null || controlPoints.Length < 4)
            return;

        int numCurves = controlPoints.Length - 3;
        int totalSegments = numCurves * segmentResolution;

        // +1 to include the very last endpoint
        line.positionCount = totalSegments + 1;

        int index = 0;

        for (int i = 0; i < numCurves; i++)
        {
            Vector3 p0 = controlPoints[i].position;
            Vector3 p1 = controlPoints[i + 1].position;
            Vector3 p2 = controlPoints[i + 2].position;
            Vector3 p3 = controlPoints[i + 3].position;

            for (int s = 0; s < segmentResolution; s++)
            {
                float t = s / (float)segmentResolution;
                line.SetPosition(index, CatmullRom(p0, p1, p2, p3, t));
                index++;
            }
        }

        // final point (t = 1 of the last segment)
        Vector3 lp0 = controlPoints[controlPoints.Length - 4].position;
        Vector3 lp1 = controlPoints[controlPoints.Length - 3].position;
        Vector3 lp2 = controlPoints[controlPoints.Length - 2].position;
        Vector3 lp3 = controlPoints[controlPoints.Length - 1].position;

        line.SetPosition(index, CatmullRom(lp0, lp1, lp2, lp3, 1f));
    }

    private void DrawCatmullRomFromTransforms(Transform[] tPoints) 
    {
        //Convert transform into position Vector3 and pass it to ProceduralSplineDrawer / DrawCatmullRomVector3Array
        Vector3[] points = new Vector3[tPoints.Length];
        for (int i = 0; i < tPoints.Length; i++)
        {
            points[i] = tPoints[i].position;
        }

        DrawCatmullRomProceduralSpline(points);
    }
    private void DrawCatmullRomProceduralSpline(Vector3[] points) 
    {
        int numCurves = points.Length - 3;
        int totalSegments = numCurves * segmentResolution;
//ASK: WHY +1?
        line.positionCount = totalSegments + 1;

        //Storing positions of each per each curve segment 
        int index = 0;
        for (int i = 0; i < numCurves; i++)
        {
            Vector3 p0 = points[i];
            Vector3 p1 = points[i + 1];
            Vector3 p2 = points[i + 2];
            Vector3 p3 = points[i + 3];

            //Evaluate t on each catmull's curve segment based on total segment/resolution count per each curve segment
            for (int s = 0; s < segmentResolution; s++)
            {
                float t = s / (float)segmentResolution;
                line.SetPosition(index, CatmullRom(p0, p1, p2, p3, t));
                index++;
            }
        }

        // last point (t=1)
        Vector3 lp0 = points[points.Length - 4];
        Vector3 lp1 = points[points.Length - 3];
        Vector3 lp2 = points[points.Length - 2];
        Vector3 lp3 = points[points.Length - 1];

        line.SetPosition(index, CatmullRom(lp0, lp1, lp2, lp3, 1f));
    }

    private void OnDrawGizmos()
    {
        // Decide which set of points to visualize
        Vector3[] pts = null;

        if (useProceduralPoints)
        {
            // In the editor, GenerateControlPointsFromAtoB can still run,
            // because it only uses Transform positions.
            if (startPoint == null || endPoint == null)
                return;

            GenerateControlPointsFromAtoB();
            pts = generatedPoints;
        }
        else
        {
            if (controlPoints == null || controlPoints.Length < 4)
                return;

            pts = new Vector3[controlPoints.Length];
            for (int i = 0; i < controlPoints.Length; i++)
            {
                if (controlPoints[i] != null)
                    pts[i] = controlPoints[i].position;
            }
        }

        if (pts == null || pts.Length < 4)
            return;
        
        //Draw spheres at control points
        Gizmos.color = Color.yellow;
        float sphereRadius = 0.065f;

        for (int i = 0; i < pts.Length; i++)
        {
            Gizmos.DrawSphere(pts[i], sphereRadius);
        }

        Gizmos.color = Color.red; 
        Gizmos.DrawSphere(pts[0], sphereRadius * 1.2f);   // p0
        Gizmos.color = Color.green; 
        Gizmos.DrawSphere(pts[1], sphereRadius * 1.2f);   // p1
        int lastP = pts.Length - 2;
        Gizmos.color = Color.green; 
        Gizmos.DrawSphere(pts[lastP], sphereRadius * 1.2f);   // pN - 2
        int endP = pts.Length - 1;
        Gizmos.color = Color.red; 
        Gizmos.DrawSphere(pts[endP], sphereRadius * 1.2f);   // pN - 1

        //Spline Color
        Gizmos.color = Color.white;
        int numCurves = pts.Length - 3;
        if (numCurves <= 0)
            return;

        Vector3 previousPos = CatmullRom(pts[0], pts[1], pts[2], pts[3], 0f);

        for (int i = 0; i < numCurves; i++)
        {
            Vector3 p0 = pts[i];
            Vector3 p1 = pts[i + 1];
            Vector3 p2 = pts[i + 2];
            Vector3 p3 = pts[i + 3];

            for (int s = 1; s <= segmentResolution; s++)
            {
                float t = s / (float)segmentResolution;
                Vector3 pos = CatmullRom(p0, p1, p2, p3, t);

                Gizmos.DrawLine(previousPos, pos);
                previousPos = pos;
            }
        }
    }
}
