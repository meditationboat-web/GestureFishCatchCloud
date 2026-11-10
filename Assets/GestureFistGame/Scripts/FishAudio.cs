using UnityEngine;
namespace GestureFistGame {
 public sealed class FishAudio:MonoBehaviour {
  AudioSource music,effects;AudioClip lift,splash,perfect,normal,fail;FishGameController game;float volume;
  void Awake(){game=GetComponent<FishGameController>();music=gameObject.AddComponent<AudioSource>();music.playOnAwake=false;music.loop=true;music.spatialBlend=0;music.clip=Resources.Load<AudioClip>("Audio/FishingLofi");music.volume=0;if(music.clip!=null)music.Play();effects=gameObject.AddComponent<AudioSource>();effects.playOnAwake=false;effects.spatialBlend=0;
   lift=Tone(450,850,.18f,.06f);splash=Tone(150,65,.3f,.5f);perfect=Tone(660,1320,.35f,.03f);normal=Tone(550,740,.24f,.05f);fail=Tone(280,120,.32f,.12f);
  }
  void Update(){float target=game.Phase==FishGameController.RoundPhase.Paused?.09f:.25f;volume=Mathf.MoveTowards(volume,target,Time.unscaledDeltaTime*.3f);music.volume=volume;}
  public void Lift(){effects.PlayOneShot(lift,.55f);}public void Splash(){effects.PlayOneShot(splash,.45f);}
  public void Result(int quality){effects.PlayOneShot(quality==2?perfect:quality==1?normal:fail,.65f);}
  static AudioClip Tone(float from,float to,float duration,float noise){int n=Mathf.CeilToInt(44100*duration);var samples=new float[n];float phase=0;var random=new System.Random(43);for(int i=0;i<n;i++){float t=i/(float)n;phase+=2*Mathf.PI*Mathf.Lerp(from,to,t)/44100;samples[i]=(Mathf.Sin(phase)*(1-noise)+(float)(random.NextDouble()*2-1)*noise)*Mathf.Sin(Mathf.PI*t)*Mathf.Exp(-2*t)*.45f;}var clip=AudioClip.Create("FishingEffect",n,1,44100,false);clip.SetData(samples,0);return clip;}
  void OnDestroy(){foreach(var clip in new[]{lift,splash,perfect,normal,fail})if(clip!=null)Destroy(clip);}
 }
}
