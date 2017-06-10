﻿namespace Aimtec.SDK.Prediction.Health
{
    public class HealthPrediction 
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HealthPrediction"/> class.
        /// </summary>
        /// <param name="impl">The implementation.</param>
        public HealthPrediction(IHealthPrediction impl)
        {
            Implementation = impl;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HealthPrediction"/> class.
        /// </summary>
        public HealthPrediction()
            : this(new HealthPredictionImplB())
        {

        }

        /// <summary>
        /// Gets or sets the implementation.
        /// </summary>
        /// <value>The implementation.</value>
        public static IHealthPrediction Implementation { get; set; }

        /// <summary>
        /// Gets the predicted health of the target.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="time">The time.</param>
        /// <returns>System.Single.</returns>
        public float GetPrediction(Obj_AI_Base target, int time)
        {
            return Implementation.GetPrediction(target, time);
        }
    }
}
