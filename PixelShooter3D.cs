using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Numerics;
using System.Linq;

public class PixelShooter3D : Form
{
    private const int WIDTH = 800;
    private const int HEIGHT = 600;
    private const float FOV = 90f;
    private const float NEAR = 0.1f;
    private const float FAR = 1000f;
    private const float MOVE_SPEED = 0.1f;
    private const float ROTATION_SPEED = 0.05f;

    private Bitmap screen;
    private Graphics g;
    private List<Mesh> worldObjects;
    private Vector3 cameraPosition;
    private float cameraRotationY;
    private float cameraRotationX;
    private bool[] keysPressed;
    private Random random;

    public PixelShooter3D()
    {
        this.Width = WIDTH;
        this.Height = HEIGHT;
        this.Text = "Pixel Shooter 3D";
        this.DoubleBuffered = true;
        this.KeyPreview = true;

        screen = new Bitmap(WIDTH, HEIGHT);
        g = Graphics.FromImage(screen);
        keysPressed = new bool[256];
        random = new Random();

        InitializeWorld();
        InitializeCamera();

        this.KeyDown += (s, e) => keysPressed[e.KeyValue] = true;
        this.KeyUp += (s, e) => keysPressed[e.KeyValue] = false;
        this.Paint += OnPaint;
        this.Load += (s, e) => Application.Idle += GameLoop;
    }

    private void InitializeWorld()
    {
        worldObjects = new List<Mesh>();

        // Add some cubes
        for (int i = 0; i < 10; i++)
        {
            float x = random.Next(-20, 20);
            float z = random.Next(-20, 20);
            worldObjects.Add(CreateCube(new Vector3(x, 0, z), 1f));
        }

        // Add some cones
        for (int i = 0; i < 5; i++)
        {
            float x = random.Next(-20, 20);
            float z = random.Next(-20, 20);
            worldObjects.Add(CreateCone(new Vector3(x, 0, z), 1f, 2f));
        }
    }

    private void InitializeCamera()
    {
        cameraPosition = new Vector3(0, 1.7f, 0);
        cameraRotationY = 0;
        cameraRotationX = 0;
    }

    private Mesh CreateCube(Vector3 position, float size)
    {
        var vertices = new List<Vector3>
        {
            new Vector3(-size, -size, -size),
            new Vector3(size, -size, -size),
            new Vector3(size, size, -size),
            new Vector3(-size, size, -size),
            new Vector3(-size, -size, size),
            new Vector3(size, -size, size),
            new Vector3(size, size, size),
            new Vector3(-size, size, size)
        };

        var faces = new List<int[]>
        {
            new int[] { 0, 1, 2, 3 }, // Front
            new int[] { 4, 5, 6, 7 }, // Back
            new int[] { 0, 4, 7, 3 }, // Left
            new int[] { 1, 5, 6, 2 }, // Right
            new int[] { 0, 1, 5, 4 }, // Bottom
            new int[] { 3, 2, 6, 7 }  // Top
        };

        return new Mesh(vertices, faces, position, Color.FromArgb(random.Next(100, 255), random.Next(100, 255), random.Next(100, 255)));
    }

    private Mesh CreateCone(Vector3 position, float radius, float height)
    {
        var vertices = new List<Vector3>();
        int segments = 8;

        // Base vertices
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)(i * 2 * Math.PI / segments);
            vertices.Add(new Vector3(
                radius * (float)Math.Cos(angle),
                -height / 2,
                radius * (float)Math.Sin(angle)
            ));
        }

        // Apex
        vertices.Add(new Vector3(0, height / 2, 0));

        var faces = new List<int[]>();
        
        // Base face
        faces.Add(Enumerable.Range(0, segments).ToArray());

        // Side faces
        for (int i = 0; i < segments; i++)
        {
            faces.Add(new int[] { i, (i + 1) % segments, segments });
        }

        return new Mesh(vertices, faces, position, Color.FromArgb(random.Next(100, 255), random.Next(100, 255), random.Next(100, 255)));
    }

    private void GameLoop(object sender, EventArgs e)
    {
        UpdateCamera();
        this.Invalidate();
    }

    private void UpdateCamera()
    {
        // Handle rotation
        if (keysPressed[(int)Keys.Left]) cameraRotationY -= ROTATION_SPEED;
        if (keysPressed[(int)Keys.Right]) cameraRotationY += ROTATION_SPEED;
        if (keysPressed[(int)Keys.Up]) cameraRotationX -= ROTATION_SPEED;
        if (keysPressed[(int)Keys.Down]) cameraRotationX += ROTATION_SPEED;

        // Clamp vertical rotation
        cameraRotationX = Math.Clamp(cameraRotationX, -1.5f, 1.5f);

        // Handle movement
        Vector3 moveDirection = Vector3.Zero;
        if (keysPressed[(int)Keys.W]) moveDirection.Z = 1;
        if (keysPressed[(int)Keys.S]) moveDirection.Z = -1;
        if (keysPressed[(int)Keys.A]) moveDirection.X = -1;
        if (keysPressed[(int)Keys.D]) moveDirection.X = 1;

        if (moveDirection != Vector3.Zero)
        {
            moveDirection = Vector3.Normalize(moveDirection);
            Matrix4x4 rotationMatrix = Matrix4x4.CreateRotationY(cameraRotationY);
            moveDirection = Vector3.Transform(moveDirection, rotationMatrix);
            cameraPosition += moveDirection * MOVE_SPEED;
        }
    }

    private void OnPaint(object sender, PaintEventArgs e)
    {
        g.Clear(Color.Black);
        RenderWorld();
        RenderHands();
        e.Graphics.DrawImage(screen, 0, 0);
    }

    private void RenderWorld()
    {
        var projectedObjects = new List<(Mesh mesh, List<Point> projectedPoints, List<int[]> faces)>();

        foreach (var obj in worldObjects)
        {
            var projectedPoints = new List<Point>();
            foreach (var vertex in obj.Vertices)
            {
                var worldPos = vertex + obj.Position;
                var projected = ProjectPoint(worldPos);
                if (projected.HasValue)
                {
                    projectedPoints.Add(projected.Value);
                }
            }

            if (projectedPoints.Count > 0)
            {
                projectedObjects.Add((obj, projectedPoints, obj.Faces));
            }
        }

        // Sort objects by distance to camera (painter's algorithm)
        projectedObjects.Sort((a, b) =>
        {
            float distA = Vector3.Distance(cameraPosition, a.mesh.Position);
            float distB = Vector3.Distance(cameraPosition, b.mesh.Position);
            return distB.CompareTo(distA);
        });

        // Draw objects
        foreach (var obj in projectedObjects)
        {
            foreach (var face in obj.faces)
            {
                if (face.Length >= 3)
                {
                    var points = face.Select(i => obj.projectedPoints[i]).ToArray();
                    g.FillPolygon(new SolidBrush(obj.mesh.Color), points);
                    g.DrawPolygon(Pens.Black, points);
                }
            }
        }
    }

    private void RenderHands()
    {
        // Simple pixel-style hands
        int handSize = 20;
        int handX = WIDTH - handSize * 2;
        int handY = HEIGHT - handSize * 2;

        // Draw gun
        g.FillRectangle(Brushes.DarkGray, handX, handY, handSize, handSize);
        g.DrawRectangle(Pens.Black, handX, handY, handSize, handSize);

        // Draw hand
        g.FillRectangle(Brushes.SandyBrown, handX + handSize, handY, handSize, handSize);
        g.DrawRectangle(Pens.Black, handX + handSize, handY, handSize, handSize);
    }

    private Point? ProjectPoint(Vector3 worldPos)
    {
        // Convert to camera space
        var cameraSpace = worldPos - cameraPosition;
        
        // Apply camera rotation
        var rotationMatrix = Matrix4x4.CreateRotationY(cameraRotationY) * 
                           Matrix4x4.CreateRotationX(cameraRotationX);
        cameraSpace = Vector3.Transform(cameraSpace, rotationMatrix);

        // Check if point is behind camera
        if (cameraSpace.Z <= 0) return null;

        // Project to screen space
        float aspectRatio = (float)WIDTH / HEIGHT;
        float fovRad = FOV * (float)Math.PI / 180f;
        float scale = 1f / (float)Math.Tan(fovRad / 2f);

        float x = cameraSpace.X * scale / (cameraSpace.Z * aspectRatio);
        float y = -cameraSpace.Y * scale / cameraSpace.Z;

        // Convert to screen coordinates
        int screenX = (int)((x + 1) * WIDTH / 2);
        int screenY = (int)((y + 1) * HEIGHT / 2);

        return new Point(screenX, screenY);
    }

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new PixelShooter3D());
    }
}

public class Mesh
{
    public List<Vector3> Vertices { get; }
    public List<int[]> Faces { get; }
    public Vector3 Position { get; }
    public Color Color { get; }

    public Mesh(List<Vector3> vertices, List<int[]> faces, Vector3 position, Color color)
    {
        Vertices = vertices;
        Faces = faces;
        Position = position;
        Color = color;
    }
} 