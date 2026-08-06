using System;

namespace LyricHover.Core
{
    public sealed class SupporterBadgeRotationState
    {
        public const double MinimumPitch = -35.0;
        public const double MaximumPitch = 35.0;

        private const double HorizontalSensitivity = 0.42;
        private const double VerticalSensitivity = 0.34;
        private const double SnapRange = 12.0;
        private const double AutoRotateDegreesPerSecond = 4.5;

        private double yawVelocity;
        private double pitchVelocity;
        private double? targetYaw;
        private double? targetPitch;
        private bool userHasInteracted;

        public SupporterBadgeRotationState(
            double initialYaw = -18.0,
            double initialPitch = -8.0,
            bool autoRotate = true,
            bool reduceMotion = false)
        {
            Yaw = initialYaw;
            Pitch = ClampPitch(initialPitch);
            AutoRotate = autoRotate;
            ReduceMotion = reduceMotion;
        }

        public double Yaw { get; private set; }

        public double Pitch { get; private set; }

        public bool AutoRotate { get; }

        public bool ReduceMotion { get; }

        public bool IsDragging { get; private set; }

        public void BeginInteraction()
        {
            IsDragging = true;
            userHasInteracted = true;
            yawVelocity = 0;
            pitchVelocity = 0;
            targetYaw = null;
            targetPitch = null;
        }

        public void ApplyDrag(
            double horizontalDelta,
            double verticalDelta,
            double elapsedSeconds = 1.0 / 60.0)
        {
            var yawDelta = horizontalDelta * HorizontalSensitivity;
            var pitchDelta = -verticalDelta * VerticalSensitivity;
            Yaw += yawDelta;
            Pitch = ClampPitch(Pitch + pitchDelta);

            var safeElapsed = Math.Max(1.0 / 240.0, elapsedSeconds);
            yawVelocity = yawDelta / safeElapsed;
            pitchVelocity = pitchDelta / safeElapsed;
        }

        public void EndInteraction()
        {
            IsDragging = false;
            if (ReduceMotion)
            {
                yawVelocity = 0;
                pitchVelocity = 0;
                SnapImmediatelyWhenNearFace();
            }
        }

        public void AnimateTo(
            double yaw,
            double pitch = 0,
            bool userInitiated = true)
        {
            targetYaw = yaw;
            targetPitch = ClampPitch(pitch);
            yawVelocity = 0;
            pitchVelocity = 0;
            userHasInteracted |= userInitiated;

            if (ReduceMotion)
            {
                Yaw = targetYaw.Value;
                Pitch = targetPitch.Value;
                targetYaw = null;
                targetPitch = null;
            }
        }

        public bool Advance(double elapsedSeconds)
        {
            if (IsDragging || elapsedSeconds <= 0)
            {
                return false;
            }

            var previousYaw = Yaw;
            var previousPitch = Pitch;
            var seconds = Math.Min(0.05, elapsedSeconds);

            if (targetYaw.HasValue)
            {
                var easing = 1.0 - Math.Exp(-10.0 * seconds);
                Yaw += (targetYaw.Value - Yaw) * easing;
                Pitch += (targetPitch.GetValueOrDefault() - Pitch) * easing;
                if (Math.Abs(targetYaw.Value - Yaw) < 0.05 &&
                    Math.Abs(targetPitch.GetValueOrDefault() - Pitch) < 0.05)
                {
                    Yaw = targetYaw.Value;
                    Pitch = targetPitch.GetValueOrDefault();
                    targetYaw = null;
                    targetPitch = null;
                }
            }
            else if (!ReduceMotion &&
                     (Math.Abs(yawVelocity) > 0.1 || Math.Abs(pitchVelocity) > 0.1))
            {
                Yaw += yawVelocity * seconds;
                Pitch = ClampPitch(Pitch + pitchVelocity * seconds);
                var damping = Math.Exp(-5.2 * seconds);
                yawVelocity *= damping;
                pitchVelocity *= damping;

                if (Math.Abs(yawVelocity) < 28.0 && TryGetNearbyFace(Yaw, out var faceYaw))
                {
                    targetYaw = faceYaw;
                    targetPitch = 0;
                    yawVelocity = 0;
                    pitchVelocity = 0;
                }
            }
            else if (!ReduceMotion && AutoRotate && !userHasInteracted)
            {
                Yaw += AutoRotateDegreesPerSecond * seconds;
            }

            return Math.Abs(previousYaw - Yaw) > 0.0001 ||
                   Math.Abs(previousPitch - Pitch) > 0.0001;
        }

        private void SnapImmediatelyWhenNearFace()
        {
            if (TryGetNearbyFace(Yaw, out var faceYaw))
            {
                Yaw = faceYaw;
                Pitch = 0;
            }
        }

        private static bool TryGetNearbyFace(double yaw, out double faceYaw)
        {
            faceYaw = Math.Round(yaw / 180.0) * 180.0;
            return Math.Abs(faceYaw - yaw) <= SnapRange;
        }

        private static double ClampPitch(double value)
        {
            return Math.Max(MinimumPitch, Math.Min(MaximumPitch, value));
        }
    }
}
