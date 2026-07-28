using System;
using System.Collections.Generic;
using UnityEngine;

namespace BallBounce.RingEscape
{
    [CreateAssetMenu(
        fileName = "SimulationColorDatabase",
        menuName = "Ball Bounce/Simulation Color Database")]
    public sealed class SimulationColorDatabase : ScriptableObject
    {
        [Serializable]
        public sealed class ColorProfile
        {
            public string Name = "New Profile";
            [Tooltip("Short note describing the palette's color relationship.")]
            public string Harmony = "Custom";
            public Gradient RingGradient = NewGradient(
                new Color(0.72f, 0.65f, 1f, 1f),
                new Color(0.72f, 0.65f, 1f, 1f));
            [ColorUsage(true, true)] public Color BallColor =
                new Color(1.32f, 0.08f, 0.62f, 1f);
            [ColorUsage(true, true)] public Color BallGlowColor =
                new Color(1.32f, 0.08f, 0.62f, 0.42f);
            public Gradient BallTrailGradient = NewTrailGradient(
                new Color(1.32f, 0.08f, 0.62f, 1f));
            [ColorUsage(true, true)] public Color CoinColor =
                new Color(1f, 0.69f, 0.08f, 1f);
            public Color PickupRadiusColor =
                new Color(1f, 0.69f, 0.08f, 0.32f);

            private static Gradient NewGradient(Color inner, Color outer)
            {
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(inner, 0f),
                        new GradientColorKey(outer, 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(inner.a, 0f),
                        new GradientAlphaKey(outer.a, 1f)
                    });
                return gradient;
            }

            private static Gradient NewTrailGradient(Color color)
            {
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(Color.Lerp(color, Color.white, 0.2f), 0f),
                        new GradientColorKey(color, 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(0.72f, 0f),
                        new GradientAlphaKey(0f, 1f)
                    });
                return gradient;
            }
        }

        [SerializeField] private List<ColorProfile> profiles = new List<ColorProfile>();

        public int ProfileCount => profiles.Count;

        public ColorProfile GetProfile(int index)
        {
            return profiles.Count == 0
                ? null
                : profiles[Mathf.Clamp(index, 0, profiles.Count - 1)];
        }
    }
}
