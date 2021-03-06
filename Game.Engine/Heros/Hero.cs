﻿namespace Game.Engine.Heros
{
    using System;
    using System.Collections.Generic;
    using Interfaces;
    using System.Reactive;
    using System.Reactive.Linq;
    using System.Reactive.Subjects;
    using Interfaces.IActions;
    using Objects;
    using States;
    using Wrapers;
    public class Hero : MobileObject, IPicker
    {
        private Subject<EventPattern<StateEventArgs>> staSubject = new Subject<EventPattern<StateEventArgs>>();

        public IState State { get; private set; }

        public uint Speed { get; set; }

        public double Angle { get; set; }

        private readonly Queue<IState> _stateQueue;

        private readonly Bag Bag;

        private readonly HeroLifeCycle _heroLifeCycle;

        internal HeroLifeCycle HeroLifeCycle
        {
            get { return _heroLifeCycle; }
        }

        private bool _isThen = false;

        public List<Point> PointList { get; private set; }

        public IObservable<EventPattern<StateEventArgs>> States
        {
            get { return staSubject; }
        }

        public Hero()
        {
            Position = new Point();
            Speed = 2;
            Angle = 0;

            Bag = new Bag();

            _heroLifeCycle = new HeroLifeCycle();

            _stateQueue = new Queue<IState>();
            PointList = new List<Point>();
            State = new Standing(this);

            Observable.FromEventPattern<StateHandler, StateEventArgs>(
                ev => StateEvent.NextState += ev,
                ev => StateEvent.NextState -= ev).Subscribe(staSubject);
            staSubject.Subscribe(x =>
            {
                if (_stateQueue.Count > 0)
                {
                    IState nextState;
                    while (_stateQueue.Count > 0)
                    {
                        nextState = _stateQueue.Dequeue();

                        if (nextState == State || nextState == null)
                            continue;

                        State = nextState;

                        return;
                    }
                }

                if (_stateQueue.Count == 0)
                {
                    State = new Standing(this);
                }
            });
        }

        public void StartMove(Point destination, Stack<Point> points)
        {
            using (new StateFirer(this))
            {
                if (points == null)
                {
                    _stateQueue.Enqueue(new Moving(this, destination));
                    return;
                }

                PointList.Clear();
                PointList.Add(Position);
                while (points.Count > 0)
                {
                    PointList.Add(points.Peek());
                    _stateQueue.Enqueue(new Moving(this, points.Pop()));
                }
            }
        }

        public void AddToBag(IEnumerable<GameObject> objects)
        {
            Bag.Add(objects);
        }

        public List<GameObject> GetContainerItems()
        {
            return Bag.GameObjects;
        }

        public Hero Then()
        {
            this._isThen = true;
            return this;
        }

        public void StartActing(IAction action, Point destination, IEnumerable<RemovableWrapper<GameObject>> objects)
        {
            using (new StateFirer(this))
            {
                _stateQueue.Enqueue(new Acting(this, action, destination, objects));
            }
        }

        class StateFirer : IDisposable
        {
            private readonly Hero _hero;
            public StateFirer(Hero hero)
            {
                _hero = hero;
                if (!_hero._isThen)
                {
                    _hero._stateQueue.Clear();
                }
                else
                    _hero._isThen = _hero._stateQueue.Count > 0;
            }
            public void Dispose()
            {
                if (!_hero._isThen)
                    StateEvent.FireEvent();

                _hero._isThen = false;
            }
        }

        public void Eat()
        {
            this.HeroLifeCycle.Eat();
        }

        public IEnumerable<KeyValuePair<string, int>> GetProperties()
        {
            return new List<KeyValuePair<string, int>>()
            {
                new KeyValuePair<string, int>("Health", _heroLifeCycle.HeroProperties.Health),
                new KeyValuePair<string, int>("Satiety", _heroLifeCycle.HeroProperties.Satiety)
            };
        }

        public void RemoveFromContainer(GameObject gObject)
        {
            Bag.GameObjects.Remove(gObject);
        }
    }
}