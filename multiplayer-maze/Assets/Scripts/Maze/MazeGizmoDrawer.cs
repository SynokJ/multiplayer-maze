using UnityEngine;

/// <summary>
/// Scene-view preview of the maze layout. Works before Generate is pressed,
/// because the layout depends only on the settings.
/// </summary>
[RequireComponent(typeof(MazeController), typeof(MazeBuilder))]
public class MazeGizmoDrawer : MonoBehaviour
{
#if UNITY_EDITOR
    private MazeData _previewData;

    private void OnDrawGizmosSelected()
    {
        var controller = GetComponent<MazeController>();
        var builder = GetComponent<MazeBuilder>();

        MazeSettings settings = controller.Settings;
        if (settings.width < 2 || settings.height < 2)
            return;

        if (_previewData == null || _previewData.width != settings.width
            || _previewData.height != settings.height || _previewData.seed != settings.seed)
            _previewData = settings.CreateData();

        float cell = builder.CellSize;
        Vector3 origin = transform.position + (Vector3)builder.GridOrigin(_previewData.width, _previewData.height);
        Vector3 right = Vector3.right * cell;
        Vector3 up = Vector3.up * cell;

        Gizmos.color = Color.cyan;
        for (int y = 0; y < _previewData.height; ++y)
            for (int x = 0; x < _previewData.width; ++x)
            {
                Vector3 corner = origin + new Vector3(x, y) * cell;

                if (_previewData.HasWall(x, y, MazeData.Wall.North))
                    Gizmos.DrawLine(corner + up, corner + up + right);
                if (_previewData.HasWall(x, y, MazeData.Wall.East))
                    Gizmos.DrawLine(corner + right, corner + right + up);
                if (y == 0 && _previewData.HasWall(x, y, MazeData.Wall.South))
                    Gizmos.DrawLine(corner, corner + right);
                if (x == 0 && _previewData.HasWall(x, y, MazeData.Wall.West))
                    Gizmos.DrawLine(corner, corner + up);
            }

        int centerX = _previewData.width / 2;
        int centerY = _previewData.height / 2;
        var farthest = _previewData.FindFarthestCell(centerX, centerY);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(builder.CellToWorld(_previewData, centerX, centerY), cell * 0.3f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(builder.CellToWorld(_previewData, farthest.x, farthest.y), cell * 0.3f);
    }
#endif
}
