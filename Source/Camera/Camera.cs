﻿using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using BluishFramework;
using System.Diagnostics;

namespace BluishEngine
{
    /// <summary>
    /// Wrapper function for a <see cref="Matrix"/> that transforms world coordinates to screen coordinates
    /// </summary>
    public class Camera
    {
        /// <summary>
        /// A <see cref="float"/> representing the zoom level, with <c>1</c> being the default zoom
        /// </summary>
        public float Zoom
        {
            get
            {
                return _zoom;
            }
            set
            {
                Vector2 centre = Viewport.Center;

                if (Bounds.HasValue)
                    _zoom = Math.Max(value, Math.Max((float)_defaultDimensions.X / Bounds.Value.Width, (float)_defaultDimensions.Y / Bounds.Value.Height));
                else
                    _zoom = value;

                FocusOn(centre);
            }
        }
        /// <summary>
        /// The location of the top-left corner of the viewport
        /// </summary>
        public Vector2 Position
        {
            get
            {
                return _position;
            }
            set
            {
                if (_canManuallyMove)
                {
                    _position = value;
                    ClampViewportToBounds();
                }  
            }
        }
        /// <summary>
        /// The viewable area of the world as a <see cref="Rectangle"/>
        /// </summary>
        public RectangleF Viewport
        {
            get
            {
                Matrix inverse = Matrix.Invert(Transform());

                Vector2 TL = Vector2.Zero;
                Vector2 BR = _defaultDimensions.ToVector2();

                TL = Vector2.Transform(TL, inverse);
                BR = Vector2.Transform(BR, inverse);

                return new RectangleF(TL, BR - TL);
            }
            set
            {
                Zoom = value.Width / _defaultDimensions.X;
                Position = value.Location;
                ClampViewportToBounds();
            }
        }
        /// <summary>
        /// An optional <see cref="Rectangle"/> restricting the range of movement of this <see cref="Camera"/>
        /// </summary>
        public Rectangle? Bounds { 
            get
            {
                return _bounds;
            }
            set
            {
                if (_canManuallyMove)
                {
                    _bounds = value;
                    ClampViewportToBounds();
                }
            }
        }

        private bool _canManuallyMove;
        private Vector2 _position;
        private float _zoom;
        private Rectangle? _bounds;
        private Point _defaultDimensions;
        private Dictionary<Type, CameraEffect> _effects;
        private List<CameraEffect> _effectsToRemove;

        public Camera(Point defaultViewportDimensions)
        {
            _defaultDimensions = defaultViewportDimensions;
            _effects = new Dictionary<Type, CameraEffect>();
            _effectsToRemove = new List<CameraEffect>();
            _canManuallyMove = true;
            Viewport = new RectangleF(Vector2.Zero, _defaultDimensions.ToVector2());
        }

        /// <summary>
        /// Encapsulates this <see cref="Camera"/> as a <see cref="Matrix"/>
        /// </summary>
        /// <returns>
        /// A <see cref="Matrix"/> that transforms world coordinates to screen coordinates
        /// </returns>
        public Matrix Transform()
        {
            return Matrix.CreateTranslation(-Position.X, -Position.Y, 0)
                 * Matrix.CreateScale(Zoom, Zoom, 1);
        }
        
        /// <summary>
        /// Sets <paramref name="centre"/> to the middle of the <see cref="Viewport"/>
        /// </summary>
        public void FocusOn(Vector2 centre)
        {
            Position = centre - Viewport.Size / 2;
        }

        public void SmoothFocusOn(GameTime gameTime, Vector2 centre, float smoothing)
        {
            Position = Vector2.SmoothStep(Position, centre - Viewport.Size / 2, (float)Math.Pow(1 - smoothing, 0.1 * gameTime.ElapsedGameTime.TotalMilliseconds));
        }

        public void Update(GameTime gameTime)
        {
            foreach (CameraEffect effect in _effects.Values)
            {
                effect.Update(gameTime);

                if (effect.Completed)
                {
                    _effectsToRemove.Add(effect);
                }
            }

            foreach (CameraEffect effect in _effectsToRemove)
            {
                if (_effects[effect.GetType()] == effect)
                {
                    _effects.Remove(effect.GetType());
                }
            }

            _effectsToRemove.Clear();
        }

        public void SlideTo(Vector2 destination, float duration, Action? OnCompleted = null)
        {
            _effects[typeof(Pan)] = new Pan(this, destination, duration, OnCompleted);
        }

        public void ZoomBy(float factor, float duration, Action? OnCompleted = null)
        {
            if (!_effects.ContainsKey(typeof(SmoothZoom)) && _canManuallyMove)
                _effects[typeof(SmoothZoom)] = new SmoothZoom(this, factor, duration, OnCompleted);
        }

        private void ClampViewportToBounds()
        {
            if (Bounds.HasValue)
            {
                _position.X = Math.Clamp(_position.X, Bounds.Value.Left, Bounds.Value.Right - (int)Viewport.Width);
                _position.Y = Math.Clamp(_position.Y, Bounds.Value.Top, Bounds.Value.Bottom - (int)Viewport.Height);
            }
        }

        class CameraEffect
        {
            public bool Completed { get; private set; }
            protected float Duration { get; private set; }
            protected float ElapsedTime { get; private set; }
            protected Camera Camera { get; private set; }

            private Action? _onCompletion;

            public CameraEffect(Camera camera, float duration, Action? OnCompletion)
            {
                Duration = duration;
                ElapsedTime = 0;
                Camera = camera;
                _onCompletion = OnCompletion;
            }

            public virtual void Update(GameTime gameTime)
            {
                ElapsedTime += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (ElapsedTime >= Duration)
                {
                    Completed = true;
                    Camera._canManuallyMove = true;
                    _onCompletion?.Invoke();
                }
            }

            protected void Stop()
            {
                ElapsedTime = Duration;
            }
        }

        class Pan : CameraEffect
        {
            private Vector2 _destination;
            private Vector2 _direction;

            public Pan(Camera camera, Vector2 destination, float duration, Action? OnCompletion) : base(camera, duration, OnCompletion)
            {
                _destination = destination;
                _direction = destination - camera.Position;
            }

            public override void Update(GameTime gameTime)
            {
                Camera._canManuallyMove = false;
                Camera._position = Vector2.SmoothStep(Camera.Position, _destination, MathHelper.SmoothStep(0, 1, ElapsedTime / Duration));
                Camera._position = new Vector2((float)Math.Round(Camera._position.X, _direction.X < 0 ? MidpointRounding.ToZero : MidpointRounding.ToPositiveInfinity), (float)Math.Round(Camera._position.Y, _direction.Y < 0 ? MidpointRounding.ToZero : MidpointRounding.ToPositiveInfinity));

                if (Camera._position == _destination)
                {
                    Stop();
                }

                base.Update(gameTime);

                if (Completed)
                {
                    Camera._position = _destination;
                }
            }
        }

        class SmoothZoom : CameraEffect
        {
            private float _factor;
            private float _initialZoom;

            public SmoothZoom(Camera camera, float factor, float duration, Action? OnCompletion) : base(camera, duration, OnCompletion)
            {
                _factor = factor;
                _initialZoom = camera.Zoom;
            }

            public override void Update(GameTime gameTime)
            {
                Camera.Zoom = MathHelper.SmoothStep(Camera.Zoom, _initialZoom * _factor, MathHelper.SmoothStep(0, 1, ElapsedTime / Duration));

                base.Update(gameTime);

                if (Completed)
                {
                    Camera.Zoom = _initialZoom * _factor;
                }
            }
        }
    }
}