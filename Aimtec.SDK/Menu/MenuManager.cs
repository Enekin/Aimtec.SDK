﻿namespace Aimtec.SDK.Menu
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;

    using Aimtec.SDK.Menu.Theme;
    using Aimtec.SDK.Menu.Theme.Default;
    using Aimtec.SDK.Util;

    internal class MenuManager : IMenu
    {
        #region Fields

        private int lastXMosPos;

        private int lastYMosPos;

        private bool visible;

        #endregion

        #region Constructors and Destructors

        private MenuManager()
        {
            RenderManager.OnRender += () => this.Render(this.Position);
            Game.OnWndProc += args => this.WndProc(args.Message, args.WParam, args.LParam);

            // TODO: Make this load from settings
            this.Theme = new DefaultMenuTheme();
            Console.WriteLine("init");
        }

        #endregion

        #region Public Properties

        public static MenuManager Instance { get; } = new MenuManager();

        public Dictionary<string, IMenuComponent> Children { get; } = new Dictionary<string, IMenuComponent>();

        public string DisplayName { get; set; } = string.Empty;

        public string InternalName { get; set; } = "AimtecSDK-RootMenu";

        public Vector2 Position { get; set; } = new Vector2(10, 10);

        public MenuTheme Theme { get; set; }

        public bool Toggled { get; set; }

        public bool Visible
        {
            get
            {
                return this.visible;
            }
            set
            {
                foreach (var child in this.Menus)
                {
                    child.Visible = value;
                }

                this.visible = true;
            }
        }

        #endregion

        #region Properties

        internal IReadOnlyList<IMenuComponent> Menus => this.Children.Values.Where(x => x is IMenu).ToList();

        #endregion

        #region Public Methods and Operators

        public IMenu Add(string id, IMenuComponent menu)
        {
            this.Children.Add(id, menu);
            return this;
        }

        public IMenu Add(IMenu menu)
        {
            return this.Add(menu.InternalName, menu);
        }

        public Rectangle GetBounds(Vector2 pos)
        {
            return new Rectangle(
                (int) pos.X,
                (int) pos.Y,
                this.Theme.MenuWidth,
                this.Theme.MenuHeight * this.Menus.Count);
        }

        public IRenderManager GetRenderManager()
        {
            return null;
        }

        public void Render(Vector2 pos)
        {
            if (!this.Visible)
            {
                return;
            }

            for (var i = 0; i < this.Menus.Count; i++)
            {
                var position = this.Position + new Vector2(0, i * this.Theme.MenuHeight);

                this.Menus[i].Position = position;
                this.Menus[i].Render(this.Menus[i].Position);
            }
        }

        public void WndProc(uint message, uint wparam, int lparam)
        {
            // Drag menu
            if (message == (int) WindowsMessages.WM_KEYDOWN && wparam == (ulong) Keys.ShiftKey)
            {
                //Console.WriteLine("visible?? key = {0}", (Keys) wparam);
                this.Visible = true;
            }

            if (message == (int) WindowsMessages.WM_KEYUP && wparam == (ulong) Keys.ShiftKey)
            {
                //Console.WriteLine("not visible?? key = {0}", (Keys) wparam);
                this.Visible = false;
            }

            foreach (var menu in this.Menus)
            {
                menu.WndProc(message, wparam, lparam);
            }
        }

        #endregion

        #region Explicit Interface Methods

        IMenu IMenu.Attach()
        {
            return this;
        }

        #endregion
    }
}