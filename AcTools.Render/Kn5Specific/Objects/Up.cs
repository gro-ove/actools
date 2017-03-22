using System;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Utils;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Objects {
    public class Up {
        private readonly Kn5RenderableDriver _driver;

        public Up(Kn5RenderableDriver driver, Kn5RenderableList steer) {
            _driver = driver;
            _steer = steer;
        }

        internal static string _ds;

        public string DebugString => _ds;

        private class Arm {
            internal Kn5RenderableList _clave;
            internal Kn5RenderableList _arm;
            internal Kn5RenderableList _forearm;
            internal Kn5RenderableList _forearmEnd;
            internal Kn5RenderableList _hand;
            internal Kn5RenderableList _index;
            internal Kn5RenderableList _thumb;

            internal Finger _indexf, _middle, _ring, _pinkie;

            internal float _armLength;
            internal float _forearmLength;
            internal float _forearmEndLength;
            //internal float _handLength;
            internal Matrix _claveMatrix;

            private float _side;

            internal class Finger {
                private readonly FingerBit[] _fingerBits;

                public Finger(params FingerBit[] fingerBits) {
                    _fingerBits = fingerBits;
                }

                private float _v = -1;

                public void Set(float grabby) {
                    if (_v == grabby) return;
                    _v = grabby;

                    for (int i = 0; i < _fingerBits.Length; i++) {
                        _fingerBits[i].Set(grabby);
                    }
                }
            }

            internal class FingerBit {
                internal readonly Kn5RenderableList Node;

                private readonly Vector3 _translation;
                private readonly Vector3 _relaxedRotation;
                private readonly Vector3 _grabbyRotation;
                private Quaternion _relaxed;
                private Quaternion _grabby;

                public FingerBit(Kn5RenderableList parent, string name, Vector3 relaxedRotation, Vector3 grabbyRotation) {
                    Node = parent.GetDummyByName(name);
                    if (Node == null) return;

                    _translation = Node.LocalMatrix.GetTranslationVector();
                    _relaxedRotation = relaxedRotation * MathF.ToRad;
                    _grabbyRotation = grabbyRotation * MathF.ToRad;
                    _relaxed = Quaternion.RotationYawPitchRoll(relaxedRotation.X, relaxedRotation.Y, relaxedRotation.Z);
                    _grabby = Quaternion.RotationYawPitchRoll(grabbyRotation.X, grabbyRotation.Y, grabbyRotation.Z);
                }

                private float _v = -1;

                public void Set(float grabby) {
                    if (_v == grabby || Node == null) return;
                    _v = grabby;

                    //node.LocalMatrix = Matrix.RotationQuaternion(_relaxed * (1- grabby) + _grabby * grabby) * Matrix.Translation(_translation);

                    var r = _relaxedRotation * (1 - grabby) + _grabbyRotation * grabby;
                    Node.LocalMatrix = Matrix.RotationYawPitchRoll(r.X, r.Y, r.Z) * Matrix.Translation(_translation);
                }
            }

            internal Arm(Kn5RenderableDriver _driver, bool isLeft) {
                var s = isLeft ? "L" : "R";
                _side = isLeft ? 1f : -1f;

                _clave = _driver.GetDummyByName($"DRIVER:RIG_Clave_{s}");
                _arm = _driver.GetDummyByName($"DRIVER:RIG_Arm_{s}");
                _forearm = _driver.GetDummyByName($"DRIVER:RIG_ForeArm_{s}");
                _forearmEnd = _driver.GetDummyByName($"DRIVER:RIG_ForeArm_END_{s}");
                _hand = _driver.GetDummyByName($"DRIVER:RIG_HAND_{s}");

                _index = _hand?.GetDummyByName(isLeft ? "DRIVER:HAND_Index1" : "DRIVER:HAND_Index4");
                _thumb = _hand?.GetDummyByName($"DRIVER:HAND_{s}_Thumb3");

                _indexf = new Finger(new[] {
                    new FingerBit(_hand, isLeft ? "DRIVER:HAND_Index1" : "DRIVER:HAND_Index4",
                            new Vector3(-3f, -5f, _side * -20.5f), new Vector3(-3f, -5f, _side * -67.5f)),
                    new FingerBit(_hand, isLeft ? "DRIVER:HAND_Index2" : "DRIVER:HAND_Index5",
                            new Vector3(-2.7f, 0.2f, _side * -10.5f), new Vector3(-2.7f, 0.2f, _side * -34f)),
                    new FingerBit(_hand, isLeft ? "DRIVER:HAND_Index3" : "DRIVER:HAND_Index6",
                            new Vector3(0.5f, -2.2f, _side * -4.5f), new Vector3(0.5f, -2.2f, _side * -32.5f))
                });

                _middle = new Finger(new[] {
                    new FingerBit(_hand, isLeft ? "DRIVER:HAND_Middle1" : "DRIVER:HAND_Middle4",
                            new Vector3(10f, -1f, _side * -20.5f), new Vector3(10f, -1f, _side * -72.5f)),
                    new FingerBit(_hand, isLeft ? "DRIVER:HAND_Middle2" : "DRIVER:HAND_Middle5",
                            new Vector3(-2f, 1f, _side * -10.5f), new Vector3(-2f, 1f, _side * -39f)),
                    new FingerBit(_hand, isLeft ? "DRIVER:HAND_Middle3" : "DRIVER:HAND_Middle6",
                            new Vector3(3.6f, -3.9f, _side * -4.5f), new Vector3(3.6f, -3.9f, _side * -32.5f))
                });

                _ring = new Finger(new[] {
                    new FingerBit(_hand, isLeft ? "DRIVER:HAND_Ring1" : "DRIVER:HAND_Ring4",
                            new Vector3(11.5f, 5f, _side * -20.5f), new Vector3(11.5f, 5f, _side * -83.5f)),
                    new FingerBit(_hand, isLeft ? "DRIVER:HAND_Ring2" : "DRIVER:HAND_Ring5",
                            new Vector3(0, 0, _side * -10.5f), new Vector3(0, 0, _side * -41f)),
                    new FingerBit(_hand, isLeft ? "DRIVER:HAND_Ring3" : "DRIVER:HAND_Ring6",
                            new Vector3(0, 0, _side * -4.5f), new Vector3(0, 0, _side * -32.5f))
                });

                _pinkie = new Finger(new[] {
                    new FingerBit(_hand, isLeft ? "DRIVER:HAND_Pinkie1" : "DRIVER:HAND_Pinkie4",
                            new Vector3(12.16f, 3.4f, _side * -20.5f), new Vector3(12.16f, 3.4f, _side * -87.5f)),
                    new FingerBit(_hand, isLeft ? "DRIVER:HAND_Pinkie2" : "DRIVER:HAND_Pinkie5",
                            new Vector3(-2f, 3f, _side * -10.5f), new Vector3(-2f, 3f, _side * -45f)),
                    new FingerBit(_hand, isLeft ? "DRIVER:HAND_Pinkie3" : "DRIVER:HAND_Pinkie6",
                            new Vector3(-1.1f, -1.9f, _side * -4.5f), new Vector3(-1.1f, -1.9f, _side * -32.5f))
                });

                Init();
            }

            private void SetFingers(float grabby) {
                _middle.Set(grabby);
                _ring.Set(grabby);
                _pinkie.Set(grabby);
            }

            internal Vector3 _approxPoint;

            void Init() {
                _arm.HighlightDummy = true;
                // _thumb3.HighlightDummy = true;
                // _clave.HighlightDummy = true;
                // _hand.HighlightDummy = true;

                _armLength = (_forearm.Matrix.GetTranslationVector() - _arm.Matrix.GetTranslationVector()).Length();
                _forearmLength = (_forearmEnd.Matrix.GetTranslationVector() - _forearm.Matrix.GetTranslationVector()).Length();
                _forearmEndLength = (_hand.Matrix.GetTranslationVector() - _forearmEnd.Matrix.GetTranslationVector()).Length();
                //_handLength = 0.1f;

                _claveMatrix = _clave.LocalMatrix;
                _approxPoint = (_clave.ParentMatrix.GetTranslationVector() + Vector3.UnitX * _side * 0.2f);
            }

            internal Vector3 GetThumbsPos() {
                // calculating position between thumbs
                return (_index.Matrix.GetTranslationVector() * 2f + _thumb.Matrix.GetTranslationVector() * 2f) / 4f;
            }

            internal Vector3 GetThumbsOffset(Matrix steerMatrix, float swRadius) {
                // calculating position between thumbs
                var thumbsPos = GetThumbsPos();

                // where is should be on the steering wheel
                var thumbsTarget = Vector3.TransformCoordinate(new Vector3(swRadius * _side, 0f, 0.01f), steerMatrix);

                // required offset
                return thumbsTarget - thumbsPos;
            }

            private float _smoothFrom = 0.96f;

            internal Vector3 _targetPoint;
            private Vector3 _up;
            private Vector3 _handUp;

            private void MoveHand(Vector3 targetPoint, Vector3 up, Vector3 handUp) {
                _targetPoint = targetPoint;
                _up = up;
                _handUp = handUp;

                // static stuff
                var handLength = _armLength + _forearmLength + _forearmEndLength;

                // approximate direction
                var approxDirection = targetPoint - _approxPoint;

                // move shoulder
                var approxDistance = approxDirection.Length();
                var shoulderFix = Smooth(Math.Max(approxDistance - handLength + 0.1f, 0f) / 0.2f);
                _clave.LocalMatrix =
                    Matrix.RotationYawPitchRoll(_side * 17f.ToRadians(), (-12f - 40f * shoulderFix).ToRadians(), (_side > 0 ? -95f : 95f).ToRadians()) *
                    Matrix.Translation(0.02f * _side, 0.11f + 0.08f * shoulderFix, 0.05f + 0.05f * shoulderFix);

                
                var armPoint = _arm.Matrix.GetTranslationVector();
                var armDelta = targetPoint - armPoint;

                // comparing distance to point with hand length
                var distance = armDelta.Length();

                var relative = distance / handLength;
                if (relative > _smoothFrom) {
                    var leftMax = 1f - _smoothFrom;
                    var left = relative - _smoothFrom;
                    var smooth = left / leftMax;
                    var smoother = -0.5f + MathF.Sqrt(1f + 3f * smooth) / 2f;

                    distance = handLength * (_smoothFrom + smoother * leftMax);
                }

                /*if (distance > handLength * _smoothFrom && distance < handLength * _smoothStretch) {
                    var left = handLength - distance;
                    var leftK = (left / (1f - _smoothFrom)).Saturate();

                    // leftK = 0 → handLength * _smoothFrom
                    // leftK + very small x → handLength * _smoothFrom + very small x
                    // leftK = _smoothFrom - 1 → handLength * _smoothStretch

                    // var stretchTo = _smoothStretch;
                    distance = handLength * _smoothFrom + leftK * (_smoothStretch - _smoothFrom) * handLength;
                }*/

                var overOffset = MathF.Sqrt(Math.Max(MathF.Pow(handLength, 2f) - MathF.Pow(distance, 2f), 0f));

                // normals: direction, bottom normal for elbow offset
                var direction = Vector3.Normalize(armDelta);

                var verticalOffsetFixed = up;
                verticalOffsetFixed.Y = _side * verticalOffsetFixed.Y.Abs();
                verticalOffsetFixed.Z += _side;
                var armBottom = Vector3.Normalize(Vector3.Cross(direction, -Vector3.Normalize(verticalOffsetFixed)));

                // moving shoulder
                var armUp = (-armBottom + Vector3.UnitY * 2f) / 3f * _side;
                var armLookAt = armPoint + Vector3.Normalize(targetPoint - armPoint) * _armLength +
                        armBottom * overOffset * _armLength / handLength;
                _arm.LookAt(armLookAt, armUp);


                _forearm.LookAt(targetPoint, Vector3.UnitY * _side);
                _forearmEnd.LookAt(targetPoint, handUp);
            }

            private void SetOnSteeringWheel(Matrix steerMatrix, float swRadius) {
                // point on the wheel, matrix
                var l = Matrix.Translation(swRadius * _side, 0, 0) * steerMatrix;
                var lPos = l.GetTranslationVector();

                // vertical offset of point on the wheel after rotation
                var armUp = Vector3.TransformNormal(Vector3.UnitX, steerMatrix);

                // approximate direction
                var approxDirection = lPos - _approxPoint;

                // move hand
                var handUp = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitX * 6f + Vector3.UnitY * _side, l));

                var handXFix = Vector3.Normalize(new Vector3(approxDirection.X, Math.Min(approxDirection.Y, 0f) * 5f, approxDirection.Z.Abs() + 0.2f)).X;
                handXFix = ((handXFix.Abs() - 0.1f) * 6f).Saturate() * handXFix.Sign() * 5f;
                var handLookAt = Vector3.TransformCoordinate(Vector3.UnitZ * 5f, steerMatrix) + // really far into the wheel
                    Vector3.UnitY * (4f + armUp.Y * 2f) * (armUp.X * 2f).Saturate() + // sort of upwards depending on how high it’s on the wheel
                    Vector3.UnitX * handXFix; // on the side to avoid breaking
                _hand.LookAt(handLookAt, handUp);

                var downK = (_side * -armUp.Y / 0.5f - 0.5f).Saturate();
                _indexf.Set(0.8f - downK * 0.3f);
                _middle.Set(0.9f - downK * 0.8f);
                _ring.Set(1f - downK);
                _pinkie.Set(1f - downK);

                if (_side > 0f) {
                    _ds = $"v: {armUp}\r\nn: {handUp}\r\nx fix: {handXFix}\r\na.p.: {_approxPoint}\r\nl.p.: {lPos}\r\na.d.: {approxDirection}";
                }

                // after moved, update target point
                var targetPoint = _hand.Matrix.GetTranslationVector() + GetThumbsOffset(steerMatrix, swRadius);
                MoveHand(targetPoint, armUp, handUp);

                // move hand, again
                _hand.LookAt(handLookAt, handUp);
            }

            private void SetSwitching(Matrix steerMatrix, float swRadius, Vector3 offset) {
                MoveHand(Vector3.TransformCoordinate(new Vector3(0, 0, -0.2f), steerMatrix), Vector3.UnitY, Vector3.UnitY);
            }

            private void SetSwitchingSmooth(Matrix steerMatrix, float a) {
                var current = _targetPoint;
                var target = Vector3.TransformCoordinate(new Vector3(0, 0, -0.2f), steerMatrix);
                MoveHand(current * (1f - a) + target * a, _up, _handUp);
            }

            public void Update(float swOffset, float swRadius, Kn5RenderableCar.SteeringWheelParams swParams) {
                var steerMatrix = Matrix.Translation(0, 0, swOffset) * Matrix.RotationZ(swParams.RotationDegress.ToRadians()) * swParams.OriginalLocalMatrix
                        * swParams.ParentMatrix;

                SetOnSteeringWheel(steerMatrix, swRadius);
                /*return;

                if (_side < 0) {
                    MoveHand(Vector3.TransformCoordinate(new Vector3(0, 0, -0.8f), steerMatrix), Vector3.UnitY, Vector3.UnitY);
                    return;
                }

                var angle = ((swParams.RotationDegress + 180f) % 360 + 360) % 360 - 180f;

                // vertical offset of point on the wheel after rotation

                if (angle > 150f) {
                    var verticalOffset = Vector3.TransformNormal(Vector3.UnitX, steerMatrix);
                    steerMatrix = Matrix.Translation(0, 0, swOffset) * Matrix.RotationZ(150f.ToRadians()) * swParams.OriginalLocalMatrix
                            * swParams.ParentMatrix;

                    SetOnSteeringWheel(steerMatrix, swRadius);
                    SetSwitchingSmooth(steerMatrix, (angle - 150f) / 30f);
                } else if (angle < -90f) {
                    var verticalOffset = Vector3.TransformNormal(Vector3.UnitX, steerMatrix);
                    SetSwitching(steerMatrix, swRadius, verticalOffset);
                } else {
                    SetOnSteeringWheel(steerMatrix, swRadius);
                }

                _ds = $"{angle:F1}°";*/
            }
        }

        private bool _up;
        private Kn5RenderableList _steer;
        private Kn5RenderableList _l;
        private Kn5RenderableList _r;
        private Kn5RenderableList _dbg0;
        private Kn5RenderableList _dbg1;

        private Arm _la, _ra;

        // morgan
        //private float _swRadius = 0.174f;
        //private float _swOffset = 0f;

        // x-bow
        private float _swRadius = 0.142f;
        private float _swOffset = -0.12f;

        private void UPinit() {
            if (!_up) {
                _up = true;
                
                _l = new Kn5RenderableList(Kn5Node.CreateBaseNode("L"), null) {
                    LocalMatrix = Matrix.Translation(_swRadius, 0, _swOffset),
                    HighlightDummy = true
                };
                _r = new Kn5RenderableList(Kn5Node.CreateBaseNode("R"), null) {
                    LocalMatrix = Matrix.Translation(-_swRadius, 0, _swOffset),
                    HighlightDummy = true
                };

                _dbg0 = new Kn5RenderableList(Kn5Node.CreateBaseNode("debug"), null) {
                    HighlightDummy = true
                };

                _dbg1 = new Kn5RenderableList(Kn5Node.CreateBaseNode("debug"), null) {
                    HighlightDummy = true
                };

                //_steer.Add(_l);
                //_steer.Add(_r);

                _la = new Arm(_driver, true);
                _ra = new Arm(_driver, false);
            }
        }

        private static float Smooth(float input) {
            return 0.5f - 0.5f * (MathF.PI * input.Saturate() + 1.5708f).Sin();
        }

        public void Update(float offset, Kn5RenderableCar.SteeringWheelParams steeringWheelParams) {
            UPinit();
            
            _la.Update(_swOffset, _swRadius, steeringWheelParams);
            _ra.Update(_swOffset, _swRadius, steeringWheelParams);

            _dbg0.LocalMatrix = Matrix.Translation(_ra.GetThumbsPos());
            _dbg1.LocalMatrix = Matrix.Translation(_la._targetPoint);
            //_dbg.LocalMatrix = Matrix.Translation(targetPoint);
        }

        public void Draw(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            _dbg0?.Draw(contextHolder, camera, mode);
            _dbg1?.Draw(contextHolder, camera, mode);
        }
    }
}