using System;
using System.Linq;
using UnityEngine;

using LightTracker.Attributes;

namespace LightTracker
{
    public class ModuleLightTracker : PartModule
    {
        // Setup KSPFields and set default (false) bool values

        [KSPField(isPersistant = true)]
        private bool IsTracking;

        internal enum TrackMode
        {
            TargetVessel,
            ActiveVessel
        }

        [KSPField(isPersistant = true)]
        private TrackMode SelectedTrackMode;

        [LightProperty, KSPField(guiName = "Intensity", guiActive = true, isPersistant = true), UI_FloatRange(minValue = 0f, maxValue = 10f, stepIncrement = 0.05f)]
        public float LightIntensity = 1f;

        [LightProperty, KSPField(guiName = "Range", guiActive = true, isPersistant = true), UI_FloatRange(minValue = 1f, maxValue = 5f, stepIncrement = 0.05f)]
        public float LightRange = 1f;

        [LightProperty, KSPField(guiName = "Cone Size", guiActive = true, isPersistant = true), UI_FloatRange(minValue = 1f, maxValue = 80f, stepIncrement = 1f)]
        public float LightConeAngle = 30f;

        [LightProperty, KSPField(guiName = "Light R", guiActive = true, isPersistant = true), UI_FloatRange(minValue = 0.00f, maxValue = 1f, stepIncrement = 0.05f, scene = UI_Scene.Flight)]
        public float LightColorR = 1f;

        [LightProperty, KSPField(guiName = "Light G", guiActive = true, isPersistant = true), UI_FloatRange(minValue = 0.00f, maxValue = 1f, stepIncrement = 0.05f, scene = UI_Scene.Flight)]
        public float LightColorG = 1f;

        [LightProperty, KSPField(guiName = "Light B", guiActive = true, isPersistant = true), UI_FloatRange(minValue = 0.00f, maxValue = 1f, stepIncrement = 0.05f, scene = UI_Scene.Flight)]
        public float LightColorB = 1f;

        // Tracking on/off
        [KSPEvent(guiActive = true, guiActiveEditor = true)]
        public void ToggleTracking()
        {
            IsTracking = !IsTracking;
            UpdateTrackingControl();
        }
        private void UpdateTrackingControl()
        {
            Events["ToggleTracking"].guiName = $"Tracking: {(IsTracking ? "Enabled" : "Disabled")}";
        }

        // Track mode cycle
        [KSPEvent(guiActive = true, guiActiveEditor = true)]
        public void CycleTrackMode()
        {
            SelectedTrackMode = SelectedTrackMode.Next();
            UpdateTrackModeControl();
        }
        private void UpdateTrackModeControl()
        {
            Events["CycleTrackMode"].guiName = $"Targeting: {LabelFor(SelectedTrackMode)}";
        }
        private string LabelFor(TrackMode mode)
        {
            switch (mode)
            {
                case TrackMode.ActiveVessel:
                    return "Active vessel";
                case TrackMode.TargetVessel:
                    return "Target vessel";
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }

        public override void OnStart(PartModule.StartState state)
        {
            // Initialize lights to use custom values saved from KSP fields

            var unitylight = part.GetComponentInChildren<Light>();
            var lightColor = new Color
            {
                r = LightColorR * LightIntensity,
                g = LightColorG * LightIntensity,
                b = LightColorB * LightIntensity
            };
            unitylight.range = 100 * (float)Math.Pow(LightRange, LightRange);
            unitylight.color = lightColor;
            unitylight.spotAngle = LightConeAngle;


            // Disable stock UI elements that are unnecessary from ModuleAnimateGeneric... assuming mouse will be used instead
            foreach (var module in part.FindModulesImplementing<ModuleAnimateGeneric>())
            {
                module.Fields["actionGUIName"].guiActive = false;
                module.Fields["startEventGUIName"].guiActive = false;
                module.Fields["endEventGUIName"].guiActive = false;
                module.Events["Toggle"].guiActive = false;
                module.Fields["status"].guiActive = false;
                module.Fields["actionGUIName"].guiActiveEditor = false;
                module.Fields["startEventGUIName"].guiActiveEditor = false;
                module.Fields["endEventGUIName"].guiActiveEditor = false;
                module.Events["Toggle"].guiActiveEditor = false;
                module.Fields["status"].guiActiveEditor = false;
                if (module.animationName == "lamptilt")
                {
                    module.Fields["deployPercent"].guiName = "Tilt";
                }
                else if (module.animationName == "lamprotate")
                {
                    module.Fields["deployPercent"].guiName = "Rotate";
                }
            }

            // Hook up light props to change the underlying Unity light on the fly
            var lightFieldsNames = GetType()
                .GetFields()
                .Where(prop => Attribute.IsDefined(prop, typeof(LightPropertyAttribute)))
                .Select(prop => prop.Name);

            foreach (var fieldName in lightFieldsNames)
            {
                Fields[fieldName].uiControlFlight.onFieldChanged += OnLightSettingChanged;
                Fields[fieldName].uiControlEditor.onFieldChanged += OnLightSettingChanged;
            }

            UpdateTrackingControl();
            UpdateTrackModeControl();

            base.OnStart(state);
        }


        public void LateUpdate()
        {
            // Check to make sure tracking isn't disabled and that there is a valid vessel attached to the part
            if (!IsTracking && part.vessel != null)
            {
                // Are we tracking the current target?
                if (IsTracking)
                {
                    // Check to prevent NRE's
                    if (vessel?.targetObject != null)
                    {
                        if (vessel != vessel.targetObject.GetVessel())
                        {
                            TrackTarget(vessel.targetObject.GetTransform());
                        }
                    }
                }
                // or active vessel is default case
                else
                {
                    if (vessel != FlightGlobals.ActiveVessel)
                    {
                        TrackTarget(FlightGlobals.ActiveVessel.transform);
                    }
                }
            }
        }

        private void TrackTarget(Transform target)
        {
            //set transforms to variables
            Transform baseTransform = transform.GetChild(0);
            Transform lightCanTransform = transform.GetChild(0).GetChild(0);

            //calculate the directional vector, then calculate Quaternion
            Vector3 dir = target.transform.position - lightCanTransform.position;
            Quaternion lookRot = Quaternion.LookRotation(dir, lightCanTransform.up);

            //set the base rotation to the lookrotation and set x and z to 0 to only rotate on y
            baseTransform.rotation = lookRot;
            baseTransform.localEulerAngles = new Vector3(0, baseTransform.localEulerAngles.y, 0);

            //set the lightCanTransform rotation to the lookrotation and set the y and z to 0 to only rotate on x
            lightCanTransform.rotation = lookRot;
            lightCanTransform.localEulerAngles = new Vector3(lightCanTransform.localEulerAngles.x, 0, 0);
        }

        #region LightPropertiesLogic

        private static void ApplyLightSettings(Light light, Color color, float intensity, float range, float spotAngle)
        {
            var scaledColor = new Color
            {
                r = color.r * intensity,
                g = color.g * intensity,
                b = color.b * intensity
            };
            light.range = 100 * (float) Math.Pow(range, range);
            light.color = scaledColor;
            light.spotAngle = spotAngle;
        }

        private void ApplyLightSettings(Light light)
        {
            var color = new Color
            {
                r = LightColorR,
                g = LightColorG,
                b = LightColorB
            };
            ApplyLightSettings(light, color, LightIntensity, LightRange, LightConeAngle);
        }

        // Apply prop settings to underlying Unity light
        public void ApplyLightSettings()
        {
            var light = part.GetComponentInChildren<Light>();
            ApplyLightSettings(light);
        }

        // Read RGB values from stock KSP light module and copy to local props - for tweaking in the editor
        public void ReadStockLightColorValues()
        {
            var kspLight = part.FindModuleImplementing<ModuleLight>();
            LightColorR = kspLight.lightR;
            LightColorG = kspLight.lightG;
            LightColorB = kspLight.lightB;
        }

        // Updates underlying Unity lights with selected prop values
        // In the editor RGB settings are puled from stock KSP light module sliders
        private void OnLightSettingChanged(BaseField field, object obj)
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                ReadStockLightColorValues();

                foreach (var otherPart in part.symmetryCounterparts)
                {
                    var otherPartTracker = otherPart.FindModuleImplementing<ModuleLightTracker>();

                    otherPartTracker.ReadStockLightColorValues();
                    otherPartTracker.ApplyLightSettings();
                }
            }

            ApplyLightSettings();
        }

        #endregion
    }
}
