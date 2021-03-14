using System;
using UnityEngine;

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

        // Setup UI sliders to control lights in flight and set values

        [KSPField(guiName = "Intensity", guiActive = true, isPersistant = true), UI_FloatRange(minValue = 0f, maxValue = 10f, stepIncrement = 0.05f)]
        public float LightIntensity = 1f;

        [KSPField(guiName = "Range", guiActive = true, isPersistant = true), UI_FloatRange(minValue = 1f, maxValue = 5f, stepIncrement = 0.05f)]
        public float LightRange = 1f;

        [KSPField(guiName = "Cone Size", guiActive = true, isPersistant = true), UI_FloatRange(minValue = 1f, maxValue = 80f, stepIncrement = 1f)]
        public float LightConeAngle = 30f;

        [KSPField(guiName = "Light R", guiActive = true, isPersistant = true), UI_FloatRange(minValue = 0.00f, maxValue = 1f, stepIncrement = 0.05f, scene = UI_Scene.Flight)]
        public float LightColorR = 1f;

        [KSPField(guiName = "Light G", guiActive = true, isPersistant = true), UI_FloatRange(minValue = 0.00f, maxValue = 1f, stepIncrement = 0.05f, scene = UI_Scene.Flight)]
        public float LightColorG = 1f;

        [KSPField(guiName = "Light B", guiActive = true, isPersistant = true), UI_FloatRange(minValue = 0.00f, maxValue = 1f, stepIncrement = 0.05f, scene = UI_Scene.Flight)]
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

            // Add events to the UI sliders instead of checking every frame
            UI_FloatRange UIRangeField;

            UIRangeField = (UI_FloatRange)Fields["IntensityField"].uiControlFlight;
            UIRangeField.onFieldChanged += OnUISliderChange;

            UIRangeField = (UI_FloatRange)Fields["RangeField"].uiControlFlight;
            UIRangeField.onFieldChanged += OnUISliderChange;

            UIRangeField = (UI_FloatRange)Fields["ConeAngleField"].uiControlFlight;
            UIRangeField.onFieldChanged += OnUISliderChange;

            UIRangeField = (UI_FloatRange)Fields["RColorField"].uiControlFlight;
            UIRangeField.onFieldChanged += OnUISliderChange;

            UIRangeField = (UI_FloatRange)Fields["GColorField"].uiControlFlight;
            UIRangeField.onFieldChanged += OnUISliderChange;

            UIRangeField = (UI_FloatRange)Fields["BColorField"].uiControlFlight;
            UIRangeField.onFieldChanged += OnUISliderChange;

            UIRangeField = (UI_FloatRange)Fields["IntensityField"].uiControlEditor;
            UIRangeField.onFieldChanged += OnUISliderChange;

            UIRangeField = (UI_FloatRange)Fields["RangeField"].uiControlEditor;
            UIRangeField.onFieldChanged += OnUISliderChange;

            UIRangeField = (UI_FloatRange)Fields["ConeAngleField"].uiControlEditor;
            UIRangeField.onFieldChanged += OnUISliderChange;

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

        // Updates the Unity light object to override the normal LightModule.  Allows setting of RGB, Range, Spot angle
        private void OnUISliderChange(BaseField field, object obj)
        {
            // In flight logic, no symmetry
            if (!HighLogic.LoadedSceneIsEditor)
            {
                var lightColor = new Color
                {
                    r = LightColorR * LightIntensity,
                    g = LightColorG * LightIntensity,
                    b = LightColorB * LightIntensity
                };
                var unitylight = part.GetComponentInChildren<Light>();
                unitylight.range = 100 * (float)Math.Pow(LightRange, LightRange);
                unitylight.color = lightColor;
                unitylight.spotAngle = LightConeAngle;
            }

            // Handle Symmetry in editor
            else
            {
                var modulelight = part.FindModuleImplementing<ModuleLight>();

                var lightColor = new Color
                {
                    r = modulelight.lightR * LightIntensity,
                    g = modulelight.lightG * LightIntensity,
                    b = modulelight.lightB * LightIntensity
                };
                var unitylight = part.GetComponentInChildren<Light>();
                unitylight.range = 100 * (float)Math.Pow(LightRange, LightRange);
                unitylight.color = lightColor;
                unitylight.spotAngle = LightConeAngle;
                LightColorR = modulelight.lightR;
                LightColorG = modulelight.lightG;
                LightColorB = modulelight.lightB;

                foreach (var part in part.symmetryCounterparts)
                {
                    var lighttrackermodule = part.FindModuleImplementing<ModuleLightTracker>();
                    unitylight = part.GetComponentInChildren<Light>();
                    unitylight.range = 100 * (float)Math.Pow(LightRange, LightRange);
                    unitylight.color = lightColor;
                    unitylight.spotAngle = LightConeAngle;
                    lighttrackermodule.LightColorR = modulelight.lightR;
                    lighttrackermodule.LightColorG = modulelight.lightG;
                    lighttrackermodule.LightColorB = modulelight.lightB;
                }
            }
        }
    }
}
