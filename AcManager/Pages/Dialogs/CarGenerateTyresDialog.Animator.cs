using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AcManager.Tools.Tyres;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Dialogs {
    public partial class CarGenerateTyresDialog {
        private class Animator {
            public Point Offset;

            private readonly CarGenerateTyresDialog _that;
            private TranslateTransform _translate, _burningTranslate, _scanTranslate;
            private RotateTransform _rotate, _burningRotate;

            public Animator(CarGenerateTyresDialog that) {
                _that = that;
                that.Loaded += (sender, args) => Run();
            }

            private void Run() {
                _size = new Point(_that.GeneratedSet.ActualWidth, _that.GeneratedSet.ActualHeight);
                _bounds = new Rect(0, 0, _that.TestElement.ActualWidth, _that.TestElement.ActualHeight);
                _translate = new TranslateTransform();
                _rotate = new RotateTransform();
                _that.GeneratedSet.RenderTransform = new TransformGroup { Children = { _rotate, _translate } };

                _burningTranslate = new TranslateTransform();
                _burningRotate = new RotateTransform();
                _that.BurningSet.RenderTransform = new TransformGroup { Children = { _burningRotate, _burningTranslate } };

                _scanTranslate = new TranslateTransform();
                _that.ScanAnimationPiece.RenderTransform = _scanTranslate;

                CompositionTargetEx.Rendering += OnRendering;
                _that.OnActualUnload(() => { CompositionTargetEx.Rendering -= OnRendering; });
            }

            private readonly TimeSpan _time = TimeSpan.Zero;
            private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
            private int _revertStage;
            private bool _running;
            private Func<TyresSet> _info;
            private TyresSet _existing;
            private double _customSpeed, _customSpeedTimer, _generatedTimer = -1;

            private const double ScanSpeed = 800;
            private const double ScanLimit = 395;
            private const double ScanAssignPosition = 80;
            private const double ScanOpacityLimit = 390;
            private const double ScanSpawnPosition = 360;

            private void OnRendering(object sender, RenderingEventArgs e) {
                if (_time == e.RenderingTime) return;
                var dt = _stopwatch.Elapsed.TotalSeconds.Clamp(0.001, 1);
                _stopwatch.Restart();

                UpdateFlames(dt);

                if (_generatedTimer > -1) {
                    _generatedTimer += dt * 3;

                    SoFresh(_that.GeneratedFresh1, _generatedTimer);
                    SoFresh(_that.GeneratedFresh2, _generatedTimer - 0.25);

                    void SoFresh(UIElement element, double value) {
                        element.Opacity = (value > 1d ? 2 - value : value).Saturate() * 0.03;
                    }
                }

                if (!_running) {
                    _that.ScanAnimationPiece.Opacity = 0d;
                    _that.GlowEffect.Opacity = 0d;
                    return;
                }

                if (_revertStage == 10) {
                    if (_customSpeed == 0d || (_customSpeedTimer += dt) > 0.1) {
                        _customSpeed = ScanSpeed * MathUtils.Random(0.2, 0.5);
                        _customSpeedTimer = 0d;
                    }

                    var position = Scan(_customSpeed);
                    if (position > ScanAssignPosition + 80 || MathUtils.Random() > 0.96) {
                        SetColor(Colors.Crimson);
                        _revertStage = 11;
                    }
                    ResetPhysics();
                    return;
                }

                if (_revertStage > 10) {
                    _revertStage = _revertStage >= 30 ? 9 : _revertStage + 1;
                    ResetPhysics();
                    return;
                }

                if (_revertStage == 9) {
                    if (Scan(-2 * ScanSpeed) == 0) {
                        _revertStage = 0;
                        _running = false;
                    }
                    ResetPhysics();
                    return;
                }

                if (_revertStage == 1) {
                    if (Scan(-2 * ScanSpeed) == 0) {
                        _revertStage = 0;
                        SetColor(Colors.Cyan);
                    }
                    ResetPhysics();
                    return;
                }

                if (_revertStage == 2) {
                    if (Scan(ScanSpeed) > ScanOpacityLimit) {
                        Restart();
                        _revertStage = 0;
                    } else {
                        UpdatePhysics();
                    }
                    return;
                }

                var current = Scan(ScanSpeed);

                if (current > ScanAssignPosition && _info != null) {
                    _existing = _info();
                    _info = null;
                    _that.GeneratedPresenter.Content = _existing;
                    if (_existing == null) {
                        _revertStage = 10;
                    }
                }

                if (current < ScanSpawnPosition) {
                    ResetPhysics();
                } else {
                    UpdatePhysics();
                }

                double Scan(double dx) {
                    var value = (_scanTranslate.X + dt * dx).Clamp(0, ScanLimit);
                    _scanTranslate.X = value;

                    _that.ScanAnimationPiece.Opacity = (1d - (value - ScanSpawnPosition) / (ScanOpacityLimit - ScanSpawnPosition)).Saturate();
                    _that.GlowEffect.Opacity = _that.ScanAnimationPiece.Opacity;

                    if (value < ScanSpawnPosition) {
                        _that.GeneratedMask.Width = value;
                        _that.GeneratedMask.Height = 120;
                    } else {
                        _that.GeneratedMask.Width = 400;
                        _that.GeneratedMask.Height = 240;
                    }

                    return value;
                }

                void ResetPhysics() {
                    _stopped = 0;
                    _translate.X = 0d;
                    _translate.Y = 0d;
                    _rotate.Angle = 0d;
                }

                void UpdatePhysics() {
                    for (var i = 0; i < 10; i++) {
                        PhysicsStep(dt / 10);
                    }

                    _translate.X = _position.X - _size.X / 2d + Offset.X;
                    _translate.Y = _position.Y - _size.Y / 2d + Offset.Y;
                    _rotate.Angle = _rotation.ToDegrees();
                }
            }

            private void SetColor(Color color) {
                _that.GlowEffect.Color = color;
                _that.ScanAnimationPiece.Background = new SolidColorBrush(color);
            }

            private bool _fireActive;
            private double _burningAngularSpeed;

            private void Restart() {
                if (_existing != null) {
                    _fireActive = true;
                    _burningAngularSpeed = _angularVelocity + _rotate.Angle + MathUtils.Random(-10d, 10d);
                    _that.BurningPresenter.Content = _existing;
                    _that.BurningSet.Opacity = 1d;
                    _that.BurningBlack.Opacity = 0d;
                    _burningTranslate.X = _translate.X;
                    _burningTranslate.Y = _translate.Y;
                    _burningRotate.Angle = _rotate.Angle;
                    _existing = null;
                }

                _generatedTimer = -1;
                _that.GeneratedFresh1.Opacity = 0d;
                _that.GeneratedFresh2.Opacity = 0d;

                _position = new Point(_bounds.Width / 2, _size.Y / 2);
                _velocity = new Point();
                _rotation = 0d;
                _angularVelocity = MathUtils.Random() < 0.3 ? MathUtils.Random(0.1, 0.7d).ToRadians() : MathUtils.Random(-1d, -0.2).ToRadians();
                _scanTranslate.X = 0d;
                SetColor(Colors.Cyan);
                _running = true;
                _revertStage = 0;
            }

            public void Spawn(Func<TyresSet> info) {
                _info = info;
                if (_running && _scanTranslate.X < ScanOpacityLimit) {
                    _revertStage = _scanTranslate.X < ScanSpawnPosition ? 1 : 2;
                } else {
                    Restart();
                }
            }

            private abstract class FireParticleBase {
                public bool IsActive;
                protected Point Position, Velocity;
                protected double Rotation, AngularVelocity;
                protected double ScaleValue, ScaleVelocity;
                protected double StartingPosition;

                private FrameworkElement _element;
                private RotateTransform _rotate;
                private ScaleTransform _scale;
                private TranslateTransform _translate;
                protected SolidColorBrush Brush;

                protected virtual CornerRadius CornerRadius => new CornerRadius(4d);
                protected virtual Color Color => ColorExtension.FromHsb(MathUtils.Random(20d, 80d), 1d, 1d);

                protected abstract void Initialize(Canvas parent);
                protected abstract void Move(double dt);
                protected abstract double UpdateState();

                public void Update(double dt, Canvas parent) {
                    if (!IsActive) {
                        IsActive = true;
                        Initialize(parent);
                    }

                    Move(dt);
                    AddTo(Velocity, dt, ref Position);
                    Rotation += AngularVelocity * dt;
                    ScaleValue += ScaleVelocity * dt;

                    if (_element == null) {
                        _translate = new TranslateTransform();
                        _rotate = new RotateTransform();
                        _scale = new ScaleTransform();

                        Brush = new SolidColorBrush(Color);
                        _element = new Border {
                            CornerRadius = CornerRadius,
                            Width = 20d,
                            Height = 20d,
                            Background = Brush,
                            RenderTransformOrigin = new Point(0.5, 0.5),
                            RenderTransform = new TransformGroup {
                                Children = {
                                    _rotate,
                                    _scale,
                                    _translate
                                }
                            }
                        };

                        parent.Children.Add(_element);
                    }

                    _translate.X = Position.X - 10d;
                    _translate.Y = Position.Y - 10d;
                    _rotate.Angle = Rotation.ToDegrees();
                    _scale.ScaleX = _scale.ScaleY = ScaleValue;
                    var value = UpdateState();
                    _element.Opacity = value.Saturate();
                    if (value <= 0d) {
                        IsActive = false;
                    }
                }
            }

            private class FlameParticle : FireParticleBase {
                protected override void Initialize(Canvas parent) {
                    AngularVelocity = MathUtils.Random(-1d, 1d);
                    ScaleValue = 1;
                    ScaleVelocity = 2;
                    Position = new Point(parent.ActualWidth * MathUtils.Random(0.05, 0.95), parent.ActualHeight + 10);
                    Rotation = MathUtils.Random(Math.PI);
                    StartingPosition = Position.Y;
                    Velocity = new Point();
                }

                protected override void Move(double dt) {
                    Velocity.Y -= 500 * dt;
                }

                protected override double UpdateState() {
                    var passed = ((Position.Y - 120) / (StartingPosition - 120)).Saturate();
                    Brush.Color = ColorExtension.FromHsb(60d * Math.Pow(passed, 1.3), 1d, Math.Pow(passed, 0.7));
                    return passed <= 0 ? -1d : Math.Pow(passed, 0.7);
                }
            }

            private class SparkParticle : FireParticleBase {
                protected override CornerRadius CornerRadius => new CornerRadius(10d);
                protected override Color Color => Colors.Yellow;
                private double _time;

                protected override void Initialize(Canvas parent) {
                    _time = 0d;
                    Position = new Point(parent.ActualWidth * MathUtils.Random(0.05, 0.95), parent.ActualHeight + 10);
                    StartingPosition = Position.Y;
                    Velocity = new Point(0, -200);
                }

                protected override void Move(double dt) {
                    _time += dt;
                    Velocity.X += MathUtils.Random(-1e3, 1e3) * dt;
                    Velocity.Y += (MathUtils.Random(-1e3, 1e3) - 100) * dt;
                    AddTo(Velocity, -0.02, ref Velocity);
                }

                protected override double UpdateState() {
                    if (Position.Y < -10 || _time > 2.25) return -1d;
                    ScaleValue = 0.1 * (1 - ((_time - 2) * 4).Saturate());
                    return 1d;
                }
            }

            private readonly FireParticleBase[] _flameParticles = 160.CreateArrayOfType<FireParticleBase, FlameParticle>();
            private readonly FireParticleBase[] _sparks = 40.CreateArrayOfType<FireParticleBase, SparkParticle>();

            private void UpdateFlames(double dt) {
                var parent = _that.FlamesAnimationParent;
                Update(_flameParticles, _fireActive ? 4 : 0);
                Update(_sparks, _fireActive && MathUtils.Random() > 0.95 ? 1 : 0);

                if (_fireActive) {
                    _burningTranslate.Y += 140 * dt;
                    _burningRotate.Angle += _burningAngularSpeed * dt;
                    var value = (_that.BurningBlack.Opacity + 2 * dt).Saturate();
                    _that.BurningBlack.Opacity = value;
                    _that.BurningSet.Opacity = 1 - value * value;
                    if (value >= 1) {
                        _fireActive = false;
                    }
                }

                void Update(FireParticleBase[] array, int activatePerFrame) {
                    activatePerFrame = (activatePerFrame * 60 * dt).Ceiling().RoundToInt();
                    for (var i = array.Length - 1; i >= 0; i--) {
                        var activated = array[i].IsActive;
                        if (activated || activatePerFrame-- > 0) {
                            array[i].Update(dt, parent);
                        }
                    }
                }
            }

            private Point _position, _velocity, _size;
            private Rect _bounds;
            private double _rotation, _angularVelocity;

            private void PhysicsStep(double dt) {
                if (_stopped > 200) {
                    if (_generatedTimer == -1 && !_fireActive) {
                        _generatedTimer = 0;
                    }
                    return;
                }

                dt = dt.Clamp(0.001, 1) * 60;
                _velocity.Y += 0.5 * dt;
                AddTo(_velocity, dt, ref _position);
                AddTo(_velocity, -0.005, ref _velocity);
                _rotation += _angularVelocity * dt;
                _angularVelocity -= _angularVelocity * (0.02 * dt).Saturate();
                double s = Math.Sin(_rotation), c = Math.Cos(_rotation);
                var o = new Point();
                var h = 0;
                TestPoint(s, c, -0.5, -0.5, ref o, ref h);
                TestPoint(s, c, 0.5, -0.5, ref o, ref h);
                TestPoint(s, c, -0.5, 0.5, ref o, ref h);
                TestPoint(s, c, 0.5, 0.5, ref o, ref h);
                AddTo(o, h > 0 ? 1d / h : 0, ref _position);

                if (_velocity.X < 0.1 && _velocity.Y < 0.1 && _angularVelocity < 0.1 && _rotation.Abs() < 0.1) {
                    _stopped = Math.Max(_stopped + (h > 0 ? 1 : -1), 0);
                } else {
                    _stopped = 0;
                }
            }

            private void TestPoint(double s, double c, double x, double y, ref Point o, ref int h) {
                var local = Rotate(s, c, _size.X * x, _size.Y * y);
                double xW = local.X + _position.X, yW = local.Y + _position.Y;
                var bound = xW < _bounds.Left ? _bounds.Left : xW > _bounds.Right ? _bounds.Right : 0;
                if (bound != 0) {
                    Resolve(new Point(bound - _position.X, local.Y), new Point(bound - xW, 0), ref o, ref h);
                }
                if (yW > _bounds.Bottom) {
                    Resolve(new Point(local.X, _bounds.Bottom - _position.Y), new Point(0, _bounds.Bottom - yW), ref o, ref h);
                }
            }

            private void Resolve(Point l, Point w, ref Point o, ref int h) {
                h++;
                AddTo(w, 1, ref o);

                var d = Rotate(_angularVelocity * 0.01, 1d, l.X, l.Y);
                var p = -(w.X * (_velocity.X + (d.X - l.X) / 0.01) + w.Y * (_velocity.Y + (d.Y - l.Y) / 0.01)) / (w.X * w.X + w.Y * w.Y);
                AddTo(w, 1.6 * p * l.Y / Length(l), ref _velocity);
                _angularVelocity += Cross(l, w) * p / 3e4;
            }

            private static double Cross(Point a, Point b) => a.X * b.Y - a.Y * b.X;
            private static double Length(Point p) => Math.Sqrt(p.X * p.X + p.Y * p.Y) + 0.00001;
            private static Point Rotate(double s, double c, double x, double y) => new Point(x * c - y * s, x * s + y * c);

            private static void AddTo(Point p, double m, ref Point t) {
                t.X += p.X * m;
                t.Y += p.Y * m;
            }

            private int _stopped;
        }
    }
}