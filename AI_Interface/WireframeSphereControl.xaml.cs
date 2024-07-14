using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace LocalGPTGui
{
    public partial class WireframeSphereControl : UserControl
    {
        private const int NumPointsOuter = 500;
        private const int NumPointsInner = 1000;
        private const double RadiusOuter = 50;
        private const double RadiusInner = 40;
        private const double MovementSpeed = 0.125;
        private const int MaxConnections = 3;
        private const double ConnectionThreshold = 10; // Increased to allow for connections between spheres
        private const double LightningProbability = 0.50; // Probability of creating a "lightning" connection

        private List<Point3D> pointsOuter = new List<Point3D>();
        private List<Point3D> pointsInner = new List<Point3D>();
        private List<Point3D> targetPointsOuter = new List<Point3D>();
        private List<Point3D> targetPointsInner = new List<Point3D>();
        private List<Tuple<int, int, bool>> connections = new List<Tuple<int, int, bool>>(); // (index1, index2, isLightning)
        private Random random = new Random();

        private GeometryModel3D pointsModelOuter;
        private GeometryModel3D pointsModelInner;
        private GeometryModel3D linesModel;

        private DispatcherTimer animationTimer;
        private DispatcherTimer targetChangeTimer;

        private Color innerSphereColor = Colors.Cyan;
        private Color outerSphereColor = Colors.Cyan;
        private Color normalConnectionColor = Color.FromArgb(100, 0, 255, 255);
        private Color lightningColor = Colors.Yellow;

        public WireframeSphereControl()
        {
            InitializeComponent();
            Loaded += WireframeSphereControl_Loaded;
        }

        private void WireframeSphereControl_Loaded(object sender, RoutedEventArgs e)
        {
            CreateAbstractSpheres();
            AnimateSpheres();
            StartPointMovementAnimation();
            StartTargetChangeTimer();
        }

        private void CreateAbstractSpheres()
        {
            CreateSphere(pointsOuter, NumPointsOuter, RadiusOuter, outerSphereColor, out pointsModelOuter);
            CreateSphere(pointsInner, NumPointsInner, RadiusInner, innerSphereColor, out pointsModelInner);

            linesModel = new GeometryModel3D();

            SphereGroup.Children.Add(pointsModelOuter);
            SphereGroup.Children.Add(pointsModelInner);
            SphereGroup.Children.Add(linesModel);

            GenerateTargetPoints(targetPointsOuter, NumPointsOuter, RadiusOuter);
            GenerateTargetPoints(targetPointsInner, NumPointsInner, RadiusInner);
        }

        private void CreateSphere(List<Point3D> points, int numPoints, double radius, Color pointColor, out GeometryModel3D pointsModel)
        {
            for (int i = 0; i < numPoints; i++)
            {
                Point3D point = GenerateRandomPointOnSphere(radius);
                points.Add(point);
            }

            pointsModel = new GeometryModel3D();
            UpdateSphereGeometry(points, pointsModel, pointColor);
        }

        private Point3D GenerateRandomPointOnSphere(double radius)
        {
            double theta = random.NextDouble() * 2 * Math.PI;
            double phi = Math.Acos(2 * random.NextDouble() - 1);
            double x = radius * Math.Sin(phi) * Math.Cos(theta);
            double y = radius * Math.Sin(phi) * Math.Sin(theta);
            double z = radius * Math.Cos(phi);
            return new Point3D(x, y, z);
        }

        private void UpdateConnections()
        {
            connections.Clear();
            List<Point3D> allPoints = new List<Point3D>();
            allPoints.AddRange(pointsInner);
            allPoints.AddRange(pointsOuter);

            for (int i = 0; i < allPoints.Count; i++)
            {
                int connectionsCount = 0;
                for (int j = i + 1; j < allPoints.Count && connectionsCount < MaxConnections; j++)
                {
                    if ((new Vector3D(allPoints[i].X - allPoints[j].X, allPoints[i].Y - allPoints[j].Y, allPoints[i].Z - allPoints[j].Z)).Length < ConnectionThreshold)
                    {
                        bool isLightning = random.NextDouble() < LightningProbability;
                        connections.Add(new Tuple<int, int, bool>(i, j, isLightning));
                        connectionsCount++;
                    }
                }
            }

            UpdateConnectionsGeometry();
        }

        private void UpdateConnectionsGeometry()
        {
            var linesMesh = new MeshGeometry3D();
            List<Point3D> allPoints = new List<Point3D>();
            allPoints.AddRange(pointsInner);
            allPoints.AddRange(pointsOuter);

            foreach (var connection in connections)
            {
                AddLine(linesMesh, allPoints[connection.Item1], allPoints[connection.Item2], 0.5);
            }

            linesModel.Geometry = linesMesh;

            var materialGroup = new MaterialGroup();
            materialGroup.Children.Add(new DiffuseMaterial(new SolidColorBrush(normalConnectionColor)));
            materialGroup.Children.Add(new EmissiveMaterial(new SolidColorBrush(normalConnectionColor)));
            linesModel.Material = materialGroup;
        }

        private void AddTriangle(MeshGeometry3D mesh, Point3D center, double size, Color color)
        {
            Vector3D up = new Vector3D(0, 1, 0);
            Vector3D right = Vector3D.CrossProduct(up, new Vector3D(center.X, center.Y, center.Z));
            right.Normalize();
            Vector3D forward = Vector3D.CrossProduct(right, up);
            forward.Normalize();

            Point3D p1 = Point3D.Add(center, Vector3D.Multiply(up, size / 2));
            Point3D p2 = Point3D.Add(center, Vector3D.Add(Vector3D.Multiply(right, size / 2 * Math.Sqrt(3) / 2), Vector3D.Multiply(up, -size / 4)));
            Point3D p3 = Point3D.Add(center, Vector3D.Add(Vector3D.Multiply(right, -size / 2 * Math.Sqrt(3) / 2), Vector3D.Multiply(up, -size / 4)));

            int startIndex = mesh.Positions.Count;
            mesh.Positions.Add(p1);
            mesh.Positions.Add(p2);
            mesh.Positions.Add(p3);

            mesh.TriangleIndices.Add(startIndex);
            mesh.TriangleIndices.Add(startIndex + 1);
            mesh.TriangleIndices.Add(startIndex + 2);
        }

        private void AddLine(MeshGeometry3D mesh, Point3D start, Point3D end, double thickness)
        {
            Vector3D direction = Point3D.Subtract(end, start);
            Vector3D perpendicular = Vector3D.CrossProduct(direction, new Vector3D(0, 1, 0));
            perpendicular.Normalize();
            perpendicular *= thickness / 2;

            Point3D p1 = Point3D.Add(start, perpendicular);
            Point3D p2 = Point3D.Subtract(start, perpendicular);
            Point3D p3 = Point3D.Add(end, perpendicular);
            Point3D p4 = Point3D.Subtract(end, perpendicular);

            int startIndex = mesh.Positions.Count;
            mesh.Positions.Add(p1);
            mesh.Positions.Add(p2);
            mesh.Positions.Add(p3);
            mesh.Positions.Add(p4);

            mesh.TriangleIndices.Add(startIndex);
            mesh.TriangleIndices.Add(startIndex + 1);
            mesh.TriangleIndices.Add(startIndex + 2);

            mesh.TriangleIndices.Add(startIndex + 1);
            mesh.TriangleIndices.Add(startIndex + 3);
            mesh.TriangleIndices.Add(startIndex + 2);
        }

        private void AnimateSpheres()
        {
            AnimateSphere(pointsModelOuter, new Vector3D(1, 1, 2), 90);
            AnimateSphere(pointsModelInner, new Vector3D(1, 2, 1), -60);
            AnimateSphere(linesModel, new Vector3D(1, 1.5, 1.5), 75);
        }

        private void AnimateSphere(GeometryModel3D model, Vector3D axis, double duration)
        {
            var rotateTransform = new RotateTransform3D();
            var axisAngleRotation = new AxisAngleRotation3D(axis, 0);
            rotateTransform.Rotation = axisAngleRotation;

            var transformGroup = new Transform3DGroup();
            transformGroup.Children.Add(rotateTransform);

            model.Transform = transformGroup;

            var animation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(Math.Abs(duration)),
                RepeatBehavior = RepeatBehavior.Forever
            };

            axisAngleRotation.BeginAnimation(AxisAngleRotation3D.AngleProperty, animation);
        }

        private void StartPointMovementAnimation()
        {
            animationTimer = new DispatcherTimer();
            animationTimer.Interval = TimeSpan.FromMilliseconds(16);// 60 FPS
            animationTimer.Tick += AnimationTimer_Tick;
            animationTimer.Start();
        }

        private void StartTargetChangeTimer()
        {
            targetChangeTimer = new DispatcherTimer();
            targetChangeTimer.Interval = TimeSpan.FromSeconds(60);
            targetChangeTimer.Tick += TargetChangeTimer_Tick;
            targetChangeTimer.Start();
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            MovePointsTowardsTargets(pointsOuter, targetPointsOuter, RadiusOuter);
            MovePointsTowardsTargets(pointsInner, targetPointsInner, RadiusInner);

            UpdateSphereGeometry(pointsOuter, pointsModelOuter, outerSphereColor);
            UpdateSphereGeometry(pointsInner, pointsModelInner, innerSphereColor);
            UpdateConnections();
        }

        private void TargetChangeTimer_Tick(object sender, EventArgs e)
        {
            GenerateTargetPoints(targetPointsOuter, NumPointsOuter, RadiusOuter);
            GenerateTargetPoints(targetPointsInner, NumPointsInner, RadiusInner);
        }

        private void GenerateTargetPoints(List<Point3D> targetPoints, int numPoints, double radius)
        {
            targetPoints.Clear();
            for (int i = 0; i < numPoints; i++)
            {
                targetPoints.Add(GenerateRandomPointOnSphere(radius));
            }
        }

        private void MovePointsTowardsTargets(List<Point3D> points, List<Point3D> targetPoints, double radius)
        {
            for (int i = 0; i < points.Count; i++)
            {
                Vector3D direction = Point3D.Subtract(targetPoints[i], points[i]);
                direction.Normalize();
                Point3D newPosition = Point3D.Add(points[i], Vector3D.Multiply(direction, MovementSpeed));

                // Project the new position back onto the sphere surface
                Vector3D toCenter = Point3D.Subtract(new Point3D(0, 0, 0), newPosition);
                toCenter.Normalize();
                points[i] = Point3D.Add(new Point3D(0, 0, 0), Vector3D.Multiply(toCenter, -radius));
            }
        }

        private void UpdateSphereGeometry(List<Point3D> points, GeometryModel3D pointsModel, Color color)
        {
            var pointsMesh = new MeshGeometry3D();
            foreach (var point in points)
            {
                AddTriangle(pointsMesh, point, 2, color);
            }
            pointsModel.Geometry = pointsMesh;

            var material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
            material.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(100, color.R, color.G, color.B))));
            pointsModel.Material = material;
        }
    }
}