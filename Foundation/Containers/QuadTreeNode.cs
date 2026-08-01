// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

using Foundation.Math;
using System;
using System.Numerics;


namespace Foundation.Containers
{
    public class QuadTreeNode<T>
    {
        public AABB Bounds;
        public List<(T Item, AABB Bounds)> Items = new List<(T Item, AABB Bounds)>();

        public QuadTreeNode<T>?[] Children = new QuadTreeNode<T>?[4];

        private const int MaxItemsBeforeSplit = 4;

        public QuadTreeNode(AABB bounds)
        {
            Bounds = bounds;
        }

        public bool IsLeaf => Children[0] == null;

        public void Split()
        {
            if (!IsLeaf) return;
            Vector3 center = (Bounds.Min + Bounds.Max) / 2;
            Children[0] = new QuadTreeNode<T>(new AABB(
                new Vector3(Bounds.Min.X, Bounds.Min.Y, Bounds.Min.Z),
                new Vector3(center.X, Bounds.Max.Y, center.Z)));

            Children[1] = new QuadTreeNode<T>(new AABB(
                new Vector3(center.X, Bounds.Min.Y, Bounds.Min.Z),
                new Vector3(Bounds.Max.X, Bounds.Max.Y, center.Z)));

            Children[2] = new QuadTreeNode<T>(new AABB(
                new Vector3(Bounds.Min.X, Bounds.Min.Y, center.Z),
                new Vector3(center.X, Bounds.Max.Y, Bounds.Max.Z)));

            Children[3] = new QuadTreeNode<T>(new AABB(
                new Vector3(center.X, Bounds.Min.Y, center.Z),
                new Vector3(Bounds.Max.X, Bounds.Max.Y, Bounds.Max.Z)));

            foreach (var entry in Items.ToList())
            {
                foreach (var child in Children)
                {
                    if (child!.Bounds.Contains(entry.Bounds.Min) && child.Bounds.Contains(entry.Bounds.Max))
                    {
                        child.Items.Add(entry);

                        Items.Remove(entry);
                        break;
                    }
                }
            }


        }
        public void Insert(T item, AABB itemBounds)
        {
            if (!Bounds.Intersects(itemBounds))
                throw new ArgumentException("Item bounds do not intersect with node bounds.");
            if (IsLeaf && Items.Count < MaxItemsBeforeSplit)
            {
                Items.Add((item, itemBounds));
            }
            else
            {
                if (IsLeaf)
                    Split();
                foreach (var child in Children)
                {
                    if (child!.Bounds.Contains(itemBounds.Min) && child.Bounds.Contains(itemBounds.Max))
                    {
                        child.Insert(item, itemBounds);
                        return;
                    }
                }
                // If no child fully contains the item, keep it in this node
                Items.Add((item, itemBounds));
            }
        }
        public void Query(AABB queryBounds, List<T> results)
        {
            if (!Bounds.Intersects(queryBounds))
                return;
            foreach (var (item, itemBounds) in Items)
            {
                if (queryBounds.Intersects(itemBounds))
                {
                    results.Add(item);
                }
            }
            if (!IsLeaf)
            {
                foreach (var child in Children)
                {
                    child!.Query(queryBounds, results);
                }
            }
        }
    }
}