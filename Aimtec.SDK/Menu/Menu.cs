﻿namespace Aimtec.SDK.Menu
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    using Aimtec.SDK.Menu.Components;
    using Aimtec.SDK.Menu.Theme;
    using Aimtec.SDK.Util;

    /// <summary>
    ///     Class Menu.
    /// </summary>
    /// <seealso cref="Aimtec.SDK.Menu.IMenu" />
    /// <seealso cref="System.Collections.IEnumerable" />
    public class Menu : MenuComponent, IMenu, IEnumerable
    {
        #region Fields

        /// <summary>
        ///     The toggled
        /// </summary>
        private bool toggled;

        /// <summary>
        ///     The visible
        /// </summary>
        private bool visible;


        #endregion

        #region Constructors and Destructors

        /// <summary>
        ///     Initializes a new instance of the <see cref="Menu" /> class.
        /// </summary>
        /// <param name="internalName">Name of the internal.</param>
        /// <param name="displayName">The display name.</param>
        public Menu(string internalName, string displayName, bool isRoot = false)
        {
            this.InternalName = internalName;
            this.DisplayName = displayName;

            this.Root = isRoot;

            this.CallingAssemblyName = $"{Assembly.GetCallingAssembly().GetName().Name}.{Assembly.GetCallingAssembly().GetType().GUID}";
        }

        #endregion

        #region Internal Properties

        internal override string Serialized { get; }

        internal int Width { get; set; }

        #endregion

        #region Public Properties

        /// <summary>
        ///     Gets the children.
        /// </summary>
        /// <value>The children.</value>
        public override Dictionary<string, MenuComponent> Children { get; } = new Dictionary<string, MenuComponent>();

        /// <summary>
        ///     Gets or sets a value indicating whether this <see cref="IMenuComponent" /> is toggled.
        /// </summary>
        /// <value><c>true</c> if toggled; otherwise, <c>false</c>.</value>
        public override bool Toggled
        {
            get => this.toggled;
            set
            {
                this.toggled = value;

                foreach (var child in this.Children.Values)
                {
                    child.Visible = value;
                }
            }
        }

        /// <summary>
        ///     Gets or sets a value indicating whether this <see cref="IMenuComponent" /> is visible.
        /// </summary>
        /// <value><c>true</c> if visible; otherwise, <c>false</c>.</value>
        public override bool Visible
        {
            get => this.visible;
            set
            {
                this.visible = value;

                if (this.Toggled)
                {
                    foreach (var child in this.Children.Values)
                    {
                        child.Visible = value;
                    }
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is a menu.
        /// </summary>
        /// <value><c>true</c> if this instance is a menu; otherwise, <c>false</c>.</value>
        public override bool IsMenu => true;

        public bool Enabled => throw new NotImplementedException();

        public int Value => throw new NotImplementedException();


        #endregion

        #region Public Indexers

        public T As<T>()
            where T : MenuComponent
        {
            throw new NotImplementedException();
        }

        public override MenuComponent this[string name] => this.GetItem(name);


        #endregion

        #region Public Methods and Operators

        /// <summary>
        ///     Adds the specified identifier.
        /// </summary>
        /// <param name="menuComponent">The menu.</param>
        /// <returns>IMenu.</returns>
        public virtual Menu Add(MenuComponent menuComponent)
        {
            if (menuComponent != null)
            {
                if (menuComponent.Root)
                {
                    throw new Exception("You cannot add a root menu to another menu.");
                }

                //Set this menu instance as its parent
                menuComponent.Parent = this;

                this.Children.Add(menuComponent.InternalName, menuComponent);

                this.UpdateWidth();

            }

            return this;
        }

        /// <summary>
        ///     Attaches this instance.
        /// </summary>
        /// <returns>IMenu.</returns>
        public virtual Menu Attach()
        {
            if (!this.Root)
            {
                throw new Exception(
                    $"You can only attach a Root Menu. If this is supposed to be your root menu, set isRoot to true in the constructor.");
            }

            this.LoadValue();

            MenuManager.Instance.Add(this);

            return this;
        }


        /// <summary>
        /// Sets this menu instance to true will make it shared resulting in all its children becoming shared.
        /// </summary>
        public Menu SetShared(bool value)
        {
            this.Shared = value;

            foreach (var item in this.Children.Values)
            {
                item.Shared = true;
            }

            return this;
        }

        /// <summary>
        ///     Gets the bounds.
        /// </summary>
        /// <param name="pos">The position.</param>
        /// <returns>Rectangle.</returns>
        public override Rectangle GetBounds(Vector2 pos)
        {
            return new Rectangle(
                (int) pos.X,
                (int) pos.Y,
                this.Parent.Width,
                MenuManager.Instance.Theme.MenuHeight);
        }

        /// <summary>
        ///     Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.</returns>
        public IEnumerator GetEnumerator()
        {
            return this.Children.GetEnumerator();
        }

  

        /// <summary>
        ///     Gets the render manager.
        /// </summary>
        /// <returns>IRenderManager.</returns>
        public override IRenderManager GetRenderManager()
        {
            return MenuManager.Instance.Theme.BuildMenuRenderer(this);
        }

        /// <summary>
        ///     Renders the specified position.
        /// </summary>
        /// <param name="position">The position.</param>
        public override void Render(Vector2 position)
        {
            if (this.Visible)
            {
                this.GetRenderManager().Render(position);
            }


            for (var i = 0; i < this.Children.Values.Count; i++)
            {
                var child = this.Children.Values.ToList()[i];
                child.Position = position
                    + new Vector2(this.Parent.Width, i * MenuManager.Instance.Theme.MenuHeight);
                child.Render(child.Position);
            }
        }

        /// <summary>
        ///     An application-defined function that processes messages sent to a window.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="wparam">Additional message information.</param>
        /// <param name="lparam">Additional message information.</param>
        public override void WndProc(uint message, uint wparam, int lparam)
        {
            if (this.Visible && message == (ulong)WindowsMessages.WM_LBUTTONUP)
            {
                var x = lparam & 0xffff;
                var y = lparam >> 16;

                if (this.GetBounds(this.Position).Contains(x, y))
                {
                    this.Toggled = !this.Toggled;

                    foreach (var m in this.Parent.Children.Values.Where(z => z.IsMenu && z.InternalName != this.InternalName))
                    {
                        m.Toggled = false;
                    }
                }
            }

            // Pass message to children
            foreach (var child in this.Children.Values)
            {
                child.WndProc(message, wparam, lparam);
            }
        }

        internal virtual void UpdateWidth()
        {
            var children = this.Children.Values;

            int maxWidth = 0;

            foreach (var child in children)
            {
                int width = 0;
                if (child is MenuList)
                {
                    var mList = child as MenuList;
                    var longestItem = mList.Items.OrderByDescending(x => x.Length).FirstOrDefault();
                    if (longestItem != null)
                    {
                        width = (int) MenuManager.Instance.TextWidth(mList.DisplayName + longestItem);
                    }
                }

                if (child is MenuKeyBind)
                {
                    var kb = child as MenuKeyBind;
                    width = (int)MenuManager.Instance.TextWidth(kb.DisplayName + "PRESS KEY");
                }

                else
                {
                    width = (int)MenuManager.Instance.TextWidth(child.DisplayName);
                }

                if (width > maxWidth)
                {
                    maxWidth = width;
                }
            }

            this.Width = (int)(maxWidth + (MenuManager.Instance.Theme.BaseMenuWidth));
        }


        internal override void LoadValue()
        {
            //Load the saved value if applicable
            foreach (var item in this.Children.Values)
            {
                item.LoadValue();
            }
        }

        #endregion

        internal override void Save()
        {
            if (!Directory.Exists(this.ConfigBaseFolder))
            {
                Directory.CreateDirectory(this.ConfigBaseFolder);
            }

            foreach (var item in this.Children.Values)
            {
                item.Save();
            }
        }
    }
}