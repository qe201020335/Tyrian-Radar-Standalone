using EFT.Interactive;

namespace Radar
{
    using System.Collections.Generic;
    using UnityEngine;

    public class Quadtree
    {
        private QuadtreeNode root;

        public Quadtree(Rect bounds, int maxDepth = 4)
        {
            root = new QuadtreeNode(bounds, maxDepth);
        }

        public void Insert(BlipLoot obj)
        {
            Vector2 point = new Vector2(obj.targetPosition.x, obj.targetPosition.z);
            root.Insert(point, obj);
        }

        public List<BlipLoot> QueryRange(Vector2 center, float radius)
        {
            return root.QueryRange(new Circle(center, radius));
        }

        public int Count()
        {
            return (root == null) ? 0 : root.Count();
        }

        public void Remove(Vector2 point, string id)
        {
            root.Remove(point, id);
        }

        public void Clear()
        {
            root.Clear();
        }
    }

    public class QuadtreeNode
    {
        private Rect bounds;
        private int maxDepth;
        private int currentDepth;
        private List<(Vector2 point, BlipLoot obj)> objects = new List<(Vector2, BlipLoot)>();
        private QuadtreeNode[] children = new QuadtreeNode[4];
        private const int MAX_OBJECTS_BEFORE_SUBDIVIDE = 10;

        public QuadtreeNode(Rect bounds, int maxDepth, int currentDepth = 0)
        {
            this.bounds = bounds;
            this.maxDepth = maxDepth;
            this.currentDepth = currentDepth;
        }

        public int Count()
        {
            int count = 0;
            if (children[0] != null)
            {
                foreach (var child in children)
                {
                    count += child.Count();
                }
            }
            else
            {
                count = objects.Count;
            }
            return count;
        }

        public void Insert(Vector2 point, BlipLoot obj)
        {
            if (!bounds.Contains(point))
            {
                return; // Point is out of bounds
            }

            if (objects.Count < MAX_OBJECTS_BEFORE_SUBDIVIDE || currentDepth == maxDepth)
            {
                objects.Add((point, obj));
                return;
            }

            if (children[0] == null)
            {
                Subdivide();
            }

            foreach (var child in children)
            {
                child.Insert(point, obj);
            }
        }

        public void Remove(Vector2 point, string id)
        {
            if (children[0] != null)
            {
                foreach (var child in children)
                {
                    if (child.bounds.Contains(point))
                    {
                        child.Remove(point, id);
                        break;
                    }
                }
            }
            else
            {
                foreach (var obj in objects)
                {
                    if (obj.obj._id == id)
                    {
                        objects.Remove(obj);
                        break;
                    }
                }
            }
        }

        public List<BlipLoot> QueryRange(Circle range)
        {
            var results = new List<BlipLoot>();

            if (!bounds.Overlaps(range.GetBounds()))
            {
                return results; // Range does not intersect the bounds of this node
            }

            foreach (var obj in objects)
            {
                if (range.Contains(obj.point))
                {
                    results.Add(obj.obj);
                }
            }

            if (children[0] != null)
            {
                foreach (var child in children)
                {
                    results.AddRange(child.QueryRange(range));
                }
            }

            return results;
        }

        private void Subdivide()
        {
            float halfWidth = bounds.width / 2f;
            float halfHeight = bounds.height / 2f;
            float x = bounds.x;
            float y = bounds.y;

            // Define the bounds for the four children
            Rect topLeftBounds = new Rect(x, y + halfHeight, halfWidth, halfHeight);
            Rect topRightBounds = new Rect(x + halfWidth, y + halfHeight, halfWidth, halfHeight);
            Rect bottomLeftBounds = new Rect(x, y, halfWidth, halfHeight);
            Rect bottomRightBounds = new Rect(x + halfWidth, y, halfWidth, halfHeight);

            // Create the four children with the new bounds
            children[0] = new QuadtreeNode(topLeftBounds, maxDepth, currentDepth + 1);
            children[1] = new QuadtreeNode(topRightBounds, maxDepth, currentDepth + 1);
            children[2] = new QuadtreeNode(bottomLeftBounds, maxDepth, currentDepth + 1);
            children[3] = new QuadtreeNode(bottomRightBounds, maxDepth, currentDepth + 1);

            // Distribute existing objects into children
            foreach (var obj in objects)
            {
                bool inserted = false;
                foreach (var child in children)
                {
                    if (child.bounds.Contains(obj.point))
                    {
                        child.Insert(obj.point, obj.obj);
                        inserted = true;
                        break;
                    }
                }
                if (!inserted)
                {
                    Debug.LogError("Object at " + obj.point + " couldn't be inserted into any child.");
                }
            }

            // Clear the objects list as they are now stored in children
            objects.Clear();
        }

        public void Clear()
        {
            objects.Clear(); // Clear all objects in the current node

            // Recursively clear all child nodes, if any
            if (children[0] != null)
            {
                for (int i = 0; i < children.Length; i++)
                {
                    children[i].Clear();
                    children[i] = null; // Remove the reference to the child node after clearing
                }
            }
        }
    }

    public struct Circle
    {
        public Vector2 center;
        public float radius;

        public Circle(Vector2 center, float radius)
        {
            this.center = center;
            this.radius = radius;
        }

        public bool Contains(Vector2 point)
        {
            return Vector2.Distance(center, point) <= radius;
        }

        public Rect GetBounds()
        {
            return new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2);
        }
    }

}
