﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Game.Engine;
using Point = Game.Engine.Point;

namespace Game
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    /// 

    public partial class MainWindow : Window
    {
        private Game.Engine.Game _game;
        private readonly WpfDrawer _drawer;
        public MainWindow()
        {
            InitializeComponent();

            _drawer = new WpfDrawer(canvas1, listBox1, heroListBox);
            _game = new Engine.Game(_drawer, (uint)canvas1.Width, (uint)canvas1.Height);
        
            CompositionTarget.Rendering += DrawSnapshot;
        }

        // Called just before frame is rendered to allow custom drawing.
        protected void DrawSnapshot(object sender, EventArgs e)
        {
            _game.DrawChanges();
        }

        private void canvas1_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _game.LClick(new Point((int)e.GetPosition(this).X, (int)e.GetPosition(this).Y));
        }

        private void canvas1_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _game.RClick(new Point((int)e.GetPosition(this).X, (int)e.GetPosition(this).Y));
        }
    }
}
