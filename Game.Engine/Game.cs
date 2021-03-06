﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using Game.Engine.Actions;
using Game.Engine.BridgeObjects;
using Game.Engine.Heros;
using Game.Engine.Interfaces;
using Game.Engine.Interfaces.IActions;
using Game.Engine.Wrapers;
using Game.Engine.Objects;
using Microsoft.Practices.Unity;

namespace Game.Engine
{
    public class Game
    {
        private Rect curRect;

        private readonly Map _map;

        private Dictionary<uint, Cell> _cellSamples;

        private LoadSaveManager loadSaveManager;

        private readonly Hero _hero;

        private readonly IDrawer _drawer;

        private IActionRepository ActionRepository { get; set; }

        private UnityContainer _unityContainer;

        public Game(IDrawer drawer, uint width, uint height)
        {            
            curRect.Width = width;
            curRect.Height = height;

            _unityContainer = new UnityContainer();
            this.RegisterInUnityContainer();

            _map = _unityContainer.Resolve<Map>();

            loadSaveManager = new LoadSaveManager();
            loadSaveManager.LoadSnapshot(_map);

            _hero = _unityContainer.Resolve<Hero>();

            ActionRepository = _unityContainer.Resolve<IActionRepository>();

            _drawer = drawer;

            var intervals = Observable.Interval(TimeSpan.FromMilliseconds(100));

            intervals.CombineLatest(_hero.States, (tick, state) => state)
                .Subscribe(x => { if (_hero.State != null) _hero.State.Act(); });
            intervals.Subscribe(_hero.HeroLifeCycle);
        }

        private void RegisterInUnityContainer()
        {
            _unityContainer.RegisterInstance(typeof (Hero), new Hero());
            _unityContainer.RegisterInstance(typeof(Map), new Map(curRect));
            _unityContainer.RegisterType(typeof(IActionRepository), typeof(ActionRepository), new ContainerControlledLifetimeManager());

            _unityContainer.RegisterTypes(
                Assembly.GetExecutingAssembly().GetTypes().Where(
                type => !type.IsAbstract && !type.IsInterface && typeof(IAction).IsAssignableFrom(type)),
                WithMappings.FromAllInterfacesInSameAssembly,//t => new[] { typeof(IAction) },
                t => t.FullName,
                WithLifetime.PerResolve);
        }

        private void LoadSettings()
        {
            Properties.Settings.Default.Reload();
        }

        private void SaveSettings()
        {
            Properties.Settings.Default.Save();
        }

        public void LClick(Point visibleDestination)
        {
            var destination = _map.GetRealDestinationFromVisibleDestination(visibleDestination);
            MoveToDest(destination);
        }

        public void RClick(Point destination)
        {
            ShowActions(destination);
        }

        private void ShowActions(Point destination)
        {
            this._drawer.DrawMenu(destination.X, destination.Y, GetActions(destination));
        }

        private IEnumerable<ClientAction> GetActions(Point visibleDestination)
        {
            var destination = _map.GetRealDestinationFromVisibleDestination(visibleDestination);
            var destObject = _map.GetObjectFromDestination(destination);

            if (destObject == null)
            {
                return new List<ClientAction>
                {
                    new ClientAction
                    {
                        Name = "Go",
                        CanDo = true,
                        Do = () => MoveToDest(destination)
                    }
                };
            }

            var possibleActions = ActionRepository.GetPossibleActions(destObject).ToList();

            // var objects = _hero.GetContainerItems().Union(new[] { gameObject }).ToList();

            var objects = new List<GameObject>(new[] {destObject});
            var removableObjects = objects.Select(go => this.PrepareRemovableObject(go, destination)).ToList();
            return possibleActions.Select(pa => new ClientAction
            {
                Name = pa.Name,
                CanDo = pa.CanDo(_hero, objects),
                Do = () => MoveAndDoAction(pa, destination, removableObjects)
            });
        }

        public void MoveToDest(Point destination)
        {
            _hero.StartMove(destination, _map.GetEasiestWay(_hero.Position, destination));
        }

        private void MoveAndDoAction(IAction action, Point destination,
            IEnumerable<RemovableWrapper<GameObject>> objects)
        {
            _hero.StartMove(destination, _map.GetEasiestWay(_hero.Position, destination));
            _hero.Then().StartActing(action, destination, objects);
        }

        private void DoAction(IAction action, IEnumerable<RemovableWrapper<GameObject>> objects)
        {
            _hero.StartActing(action, null, objects);
        }

        private RemovableWrapper<GameObject> PrepareRemovableObject(GameObject gObject, Point destination)
        {
            return new RemovableWrapper<GameObject>
            {
                GameObject = gObject,
                RemoveFromContainer = (() =>
                {
                    _map.SetObjectFromDestination(destination, null);
                })
            };
        }

        private RemovableWrapper<GameObject> PrepareRemovableObject(GameObject gObject)
        {
            return new RemovableWrapper<GameObject>
            {
                GameObject = gObject,
                RemoveFromContainer = (() =>
                {
                    _hero.RemoveFromContainer(gObject);
                })
            };
        }

        public void DrawChanges()
        {
            _map.RecalcVisibleRect(_hero.Position);

            _drawer.Clear();

            _drawer.DrawSurface(curRect.Width, curRect.Height);

            var visibleCells = Map.RectToCellRect(_map.VisibleRect);
                
            //var mapSize = _map.GetSize();
            for (int i = visibleCells.Left; i < visibleCells.Left + visibleCells.Width; i++)
            {
                for (int j = visibleCells.Top; j < visibleCells.Top+ visibleCells.Height; j++)
                {
                    var gameObject = _map.GetObjectFromCell(new Point(i, j));
                    if (gameObject != null)
                    {
                        var visibleDestination = _map.GetVisibleDestinationFromRealDestination(Map.CellToPoint(new Point(i, j)));
                        _drawer.DrawObject(gameObject.GetDrawingCode(), visibleDestination.X, visibleDestination.Y);

                    }
                }
            }

            _drawer.DrawHero(_map.GetVisibleDestinationFromRealDestination(_hero.Position), _hero.Angle, _hero.PointList.Select(p => _map.GetVisibleDestinationFromRealDestination(p)).ToList());

            var groupedItems = _hero.GetContainerItems()
                .GroupBy(go => go.Name,
                    (name, gos) =>
                        new KeyValuePair<string, Func<IEnumerable<ClientAction>>>(
                            string.Format("{0}({1})", name, gos.Count()),
                            this.GetFuncForClientActions(gos.First())));

            _drawer.DrawContainer(groupedItems);

            _drawer.DrawHeroProperties(_hero.GetProperties());
        }

        private Func<IEnumerable<ClientAction>> GetFuncForClientActions(GameObject first)
        {
            return (() =>
            {
                var possibleActions = ActionRepository.GetPossibleActions(first).ToList();

                var objects = new List<GameObject>(new[] {first});
                var removableObjects = objects.Select(go => this.PrepareRemovableObject(go)).ToList();
                return possibleActions.Select(pa => new ClientAction
                {
                    Name = pa.Name,
                    CanDo = pa.CanDo(_hero, objects),
                    Do = () => DoAction(pa, removableObjects)
                });

            });
        }
    }
}
