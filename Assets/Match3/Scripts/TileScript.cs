/*
*************************************************************************************************************************************************************
The MIT License(MIT)
Copyright(c) <year> <copyright holders>

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

zvedza ★ Software
**************************************************************************************************************************************************************
*/
using UnityEngine;
using System.Collections;

public class TileScript : MonoBehaviour
{
    #region Variables

    #region Public Variables Shown On Inspector
    public Sprite NormalTileSprite;
    public Sprite SelectedTileSprite;
    public Sprite PoweredTileSprite;
    public Sprite SelectedPoweredTileSprite;

    public Sprite[] AnimationFrames;
    public float AnimationFrameDurationInMS;
    #endregion

    #region Public Variables

    [HideInInspector]
    public Vector2 Coordinates;

    [HideInInspector]
    public int TypeOfTile;

    [HideInInspector]
    public Vector3 PositionToMove;

    [HideInInspector]
    public bool IsMoving, HasPower;

    [HideInInspector]
    public GameObject ParentGrid;

    [HideInInspector]
    public float TimeForLerp;

    [HideInInspector]
    public enum Direction { None, Up, Down, Left, Right }
    #endregion

    #region Private Variables
    private SpriteRenderer _spriteRenderer;

    private Vector3 _lastPosition;
    private float _timeLerpStarted;

    private float _animationTime;
    private bool _isExplosionTriggered;

    private bool _hintSizeTopped;
    private bool _hint;
    #endregion

    #endregion

    #region MonoBehaviour Inherited Functions
    private void Start()
    {
        _lastPosition = transform.position; _spriteRenderer = GetComponent<SpriteRenderer>();
        _spriteRenderer.sprite = NormalTileSprite;

    }
    private void Update()
    {
        if (_hint)
            if (!_hintSizeTopped)
            {
                transform.localScale = new Vector3(transform.localScale.x + (0.5f * Time.deltaTime), transform.localScale.y + (0.5f * Time.deltaTime), transform.localScale.z);
                if (transform.localScale.x > 1.25)
                    _hintSizeTopped = true;
            }
            else
            {
                transform.localScale = new Vector3(transform.localScale.x - (0.5f * Time.deltaTime), transform.localScale.y - (0.5f * Time.deltaTime), transform.localScale.z);
                if (transform.localScale.x <= 1)
                    _hintSizeTopped = false;
            }
        else if (transform.localScale != Vector3.one)
            transform.localScale = Vector3.one;



        if (_isExplosionTriggered)
        {
            _animationTime += (Time.deltaTime * 1000);
            int frameSelect = Mathf.FloorToInt(_animationTime / AnimationFrameDurationInMS);

            if (frameSelect >= AnimationFrames.Length)
                Destroy(gameObject);
            else if (AnimationFrames.Length > 0)
                _spriteRenderer.sprite = AnimationFrames[frameSelect];
            else
                Destroy(gameObject);
        }

    }
    private void FixedUpdate()
    {
        if (IsMoving)
        {
            float currentLerp = Time.time - _timeLerpStarted;
            float percent = (currentLerp / TimeForLerp) >= 1f ? 1f : (currentLerp / TimeForLerp);

            transform.position = Vector3.Lerp(transform.position, PositionToMove, percent);
            if (percent >= 1f)
            {
                transform.position = PositionToMove;
                transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, 0f);
                PositionToMove = transform.position;
                _lastPosition = transform.position;
                IsMoving = false;
            }
        }

    }
    private void OnMouseDown() { if (HasPower) _spriteRenderer.sprite = SelectedPoweredTileSprite; else _spriteRenderer.sprite = SelectedTileSprite; }
    private void OnMouseUp()
    {
        //This function determines to where the player wants to move the tile

        if (HasPower) _spriteRenderer.sprite = PoweredTileSprite; else _spriteRenderer.sprite = NormalTileSprite;

        Direction direction = Direction.None;

        Vector3 mouseUpPoint = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseUpPoint.z = 0;

        if (Mathf.Abs((mouseUpPoint - transform.position).magnitude) > 1.5f)
        {
            if (Mathf.Abs(mouseUpPoint.x - transform.position.x) > Mathf.Abs(mouseUpPoint.y - transform.position.y))
                direction = mouseUpPoint.x - transform.position.x > 0 ? Direction.Right : Direction.Left;
            else
                direction = mouseUpPoint.y - transform.position.y > 0 ? Direction.Up : Direction.Down;
            switch (direction)
            {
                case Direction.Up:
                    MoveUp();
                    break;
                case Direction.Down:
                    MoveDown();
                    break;
                case Direction.Left:
                    MoveLeft();
                    break;
                case Direction.Right:
                    MoveRight();
                    break;
                default:
                    break;
            }
        }
    }
    #endregion

    #region Public Functions
    /// <summary>
    /// Changes the sprite of the tile to the powered version of the tile, also turns on the HasPower flag
    /// </summary>
    public void EnablePowerUp()
    {
        HasPower = true;
        _spriteRenderer.sprite = PoweredTileSprite;
    }
    /// <summary>
    /// Function used to move the tile to new coordinates
    /// </summary>
    /// <param name="newPos">New coordinates where the tile is going to move</param>
    /// <param name="milliseconds">How Many time the movement should take</param>
    public void MoveTo(Vector3 newPos, float milliseconds)
    {
        PositionToMove = newPos;
        _timeLerpStarted = Time.time;
        TimeForLerp = milliseconds;
        IsMoving = true;
    }
    /// <summary>
    /// Sets the tile in the background (1 on the z axis), used when the player triggered a movement and this tile was not the trigger.
    /// </summary>
    public void MoveToBackground()
    {
        transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, 1f);
        PositionToMove.z = 1f;
    }
    /// <summary>
    /// tells the tile to kill itself
    /// </summary>
    public void TriggerExplosion()
    {
        _isExplosionTriggered = true;

        transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, -2f);
        PositionToMove.z = -2f;
    }
    /// <summary>
    /// Tells the tile to glow or not to hint the player about its existence, also the tile becomes a saint and performs miracles on the adjacent tiles... or not.
    /// </summary>
    /// <param name="enable">self explanatory, true = turn on the halo, false = turn off the halo</param>
    public void ActivateHint(bool enable)
    {
        _hint = enable;
    }
    #endregion

    #region Private Functions
    /// <summary>
    /// Tells the grid the tile wants to move up
    /// </summary>
    private void MoveUp() { ParentGrid.GetComponent<GridScript>().MoveTile(Coordinates, Direction.Up); }
    /// <summary>
    /// Tells the grid the tile wants to move down
    /// </summary>
    private void MoveDown() { ParentGrid.GetComponent<GridScript>().MoveTile(Coordinates, Direction.Down); }
    /// <summary>
    /// Tells the grid the tile wants to move left
    /// </summary>
    private void MoveLeft() { ParentGrid.GetComponent<GridScript>().MoveTile(Coordinates, Direction.Left); }
    /// <summary>
    /// Tells the grid the tile wants to move right
    /// </summary>
    private void MoveRight() { ParentGrid.GetComponent<GridScript>().MoveTile(Coordinates, Direction.Right); }
    #endregion

}