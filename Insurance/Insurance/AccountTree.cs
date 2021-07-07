using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtualCredit.Models;

namespace Insurance
{
    public class Tree<T>
    {
        public TreeNode<T> Root { get; set; }
    }


    public class TreeNode<T>
    {
        void EnumAddDescendant(TreeNode<T> parentNode, TreeNode<T> child)
        {
            if (!parentNode.descendant.Contains(child))
                parentNode.descendant.Add(child);
            if (child.Children == null || child.Children.Count <= 0)
            {
                return;
            }

            foreach (var item in child.Children)
            {
                EnumAddDescendant(parentNode, item);
            }
        }

        private List<TreeNode<T>> descendant = new List<TreeNode<T>>();
        public List<TreeNode<T>> Descendant
        {
            get
            {
                foreach (var item in Children)
                {
                    EnumAddDescendant(this, item);
                }
                return descendant;
            }
        }
        public T Instance { get; set; }

        private TreeNode<T> parent;
        public TreeNode<T> Parent
        {
            get
            {
                return parent;
            }
            set
            {
                parent = value;
                if (value != null && !parent.Children.Contains(this))
                    parent.Children.Add(this);
            }
        }
        public List<TreeNode<T>> Children { get; set; } = new List<TreeNode<T>>();
    }
}
