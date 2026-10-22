using System;
using System.Linq;
using RuneRush.UnityRuntime;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RuneRush.Editor
{
    public static class RuneRushRevisionReview
    {
        private static RuneRushGame _game;
        private static RaceHudView _hud;
        private static RuneRushXrPointerModule _pointer;
        private static Canvas _canvas;
        private static Camera _camera;
        private static int _presses;

        public static string ValidateLiveButtons()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            _game=UnityEngine.Object.FindAnyObjectByType<RuneRushGame>();
            _hud=UnityEngine.Object.FindAnyObjectByType<RaceHudView>();
            // Restore the production menu override before installing the review's world canvas.
            var presentationField=typeof(RaceHudView).GetField("_menuPresentation",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var presentation=presentationField.GetValue(_hud) as FullscreenMenuPresentation;
            if(presentation!=null)presentation.Restore();
            _camera=UnityEngine.Camera.main;
            _canvas=GameObject.Find("RuneRush UI").GetComponent<Canvas>();
            _canvas.renderMode=RenderMode.WorldSpace;
            _canvas.transform.SetPositionAndRotation(_camera.transform.position+_camera.transform.forward*2.1f,_camera.transform.rotation);
            _canvas.transform.localScale=Vector3.one*.0013f;
            var scaler=_canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;scaler.scaleFactor=1;
            _canvas.GetComponent<RectTransform>().sizeDelta=new Vector2(1920,1080);
            EventSystem system=UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            foreach(BaseInputModule module in system.GetComponents<BaseInputModule>())module.enabled=false;
            _pointer=system.GetComponent<RuneRushXrPointerModule>();
            if(_pointer==null)_pointer=system.gameObject.AddComponent<RuneRushXrPointerModule>();
            _pointer.Configure(_canvas,_camera.transform.parent);
            _pointer.enabled=false; // The review supplies controller poses through the production event path.
            _pointer.SetInteractionEnabled(true);
            foreach(Graphic g in _canvas.GetComponentsInChildren<Graphic>(true))g.raycastTarget=g.GetComponent<Button>()!=null;
            _presses=0;
            for(int cycle=0;cycle<3;cycle++)
            {
                _game.Simulation.ReturnToMenu();Sync();
                foreach(string difficulty in new[]{"Squire\nSteady Guard","Knight\nRoyal Standard","Lord\nRuthless Hunt"})
                    for(int n=0;n<10;n++)Click(difficulty,n%2);
                Click("Start Crown Cup",cycle%2);Expect(RacePhase.Countdown);
                for(int i=0;i<10;i++)
                {
                    Click("Ⅱ",i%2);Expect(RacePhase.Paused);
                    Click("Resume Ride",i%2);Expect(RacePhase.Countdown);
                }
                Click("Ⅱ",0);Click("Restart Race",0);Expect(RacePhase.Countdown);
                Click("Ⅱ",1);Click("Back to Race Hub",1);Expect(RacePhase.Menu);
                Click("Start Crown Cup",1);
                // Drive the actual simulation to its finish state; do not assign the phase directly.
                for(int step=0;step<60000 && _game.Simulation.Phase!=RacePhase.Finished;step++)
                    _game.Simulation.Step(1f/60,new RaceInputFrame{Throttle=1,Nitro=true});
                Expect(RacePhase.Finished);Sync();
                Click("Race Again",0);Expect(RacePhase.Countdown);
                for(int step=0;step<60000 && _game.Simulation.Phase!=RacePhase.Finished;step++)
                    _game.Simulation.Step(1f/60,new RaceInputFrame{Throttle=1});
                Expect(RacePhase.Finished);Sync();
                Click("Race Hub",1);Expect(RacePhase.Menu);
            }
            string result="RUNE_RUSH_LIVE_UI_PASS: cycles=3; spatial_pointer_clicks="+_presses+"; all menu/pause/finish buttons; synthetic left/right origins; editor evidence only";
            Debug.Log(result);return result;
        }

        private static void Expect(RacePhase phase)
        {
            if(_game.Simulation.Phase!=phase)throw new InvalidOperationException("Expected "+phase+", got "+_game.Simulation.Phase);
        }

        private static void Sync()
        {
            _hud.UpdateView(_game.Simulation.CreateSnapshot());
            Canvas.ForceUpdateCanvases();
            var rt=RenderTexture.GetTemporary(1024,768,24);_camera.targetTexture=rt;_camera.Render();_camera.targetTexture=null;RenderTexture.ReleaseTemporary(rt);
            Canvas.ForceUpdateCanvases();
        }

        private static void Click(string name,int hand)
        {
            Sync();
            Button button=_canvas.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name==name);
            if(button==null)throw new InvalidOperationException("Active button missing: "+name);
            Vector3 origin=_camera.transform.position+_camera.transform.right*(hand==0?-.25f:.25f)-_camera.transform.up*.25f;
            Vector3 target=button.GetComponent<RectTransform>().TransformPoint(button.GetComponent<RectTransform>().rect.center);
            Quaternion aim=Quaternion.LookRotation(target-origin,_camera.transform.up);
            _pointer.SetInteractionEnabled(true);
            int before=_pointer.ClickCount;
            _pointer.ProcessSample(origin,aim,false);
            _pointer.ProcessSample(origin,aim,true);
            _pointer.ProcessSample(origin,aim,false);
            if(_pointer.ClickCount!=before+1)throw new InvalidOperationException("Pointer click failed: "+name);
            _presses++;
        }
    }
}
