/*
*************************************************************************************************************************************************************
The MIT License(MIT)
Copyright(c) 2016 Zvedza ★ Software

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*************************************************************************************************************************************************************
Author: Cheloide https://github.com/Cheloide

Tiles where taken from http://kenney.nl/assets/puzzle-pack-2.
Explosion animations where taken from http://opengameart.org/content/candy-pack-1. 
The match sound was taken from http://freesound.org/people/volivieri/sounds/37171/.
They're all Public domain so you can do whatever you want with them.

Would be nice if you credit us in your projects.

Zvezda ★ Software
**************************************************************************************************************************************************************
*/
using UnityEngine;
using System.Collections;

public class SoundManager : MonoBehaviour
{
    #region Public Variables Shown On Inspector

    public AudioClip[] audioClip;
    public GameObject soundEmitter;

    #endregion

    /// <summary>
    /// Function to play a sound
    /// </summary>
    /// <param name="soundIndex">The index of the sound clip to play, starts from zero, if this number is greater than the number of audio clips, nothing happens</param>
    /// <param name="pitch">the pitch of the sound, default is 1 </param>
    /// <param name="loop">true to have the sound looped, default is false</param>
    /// <returns>the new SoundEmitter Gameobject</returns>
    public GameObject PlaySound(int soundIndex, float pitch = 1, bool loop = false)
    {
        if (soundIndex >= audioClip.Length)
            return null;
        GameObject go = Instantiate(soundEmitter) as GameObject;
        go.GetComponent<AudioSource>().GetComponent<AudioSource>().clip = audioClip[soundIndex];
        go.GetComponent<AudioSource>().GetComponent<AudioSource>().pitch = pitch;
        if (loop)
            go.GetComponent<AudioSource>().loop = true;
        go.transform.parent = transform;
        go.transform.localPosition = Vector3.zero;
        return go;
    }
}
