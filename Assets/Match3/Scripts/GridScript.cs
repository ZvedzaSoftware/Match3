/*
*************************************************************************************************************************************************************
The MIT License(MIT)
Copyright(c) 2016 Zvezda ★ Software

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
using System.Collections.Generic;

public class GridScript : MonoBehaviour
{

    #region Variables

    #region Public Variables Shown on Inspector
    public GameObject[] elements;

    public int height;
    public int width;
    public float xSeparationBetweenElements;
    public float ySeparationBetweenElements;

    public int scorePerTile;
    public int scoreMultiplier;
    public int score;

    public bool isTimeEnabled;
    public bool timerStartAtFirstMovement;
    public int timeLimit;
    public float timeSinceStarted;
    public float timeLeft;
    public bool timeOut;

    public bool areHintsEnabled;
    public float hintsDelayInMS;

    public bool arePowersEnabled;
    public RefillType whenNoMovesAvailable;
    public CascadeRefill CascadeBehavior;
    public float refillGridDelayInMS;
    public float DelayBetweenExplosionsInMS;
    public ShuffleRefill shuffleBehavior;
    public float shuffleDelayInMS;
    public float shuffleTimeSpentWaitingInCenterInMS;

    public float cascadeTimeDuration;
    public float tileMoveTimeDuration;
    #endregion

    #region Public Variables
    public enum RefillType { deleteAllAndCascade, Shuffle }
    public enum CascadeRefill
    {
        ExplodeAllAtOnce,
        ExplodeAtRandom,
        ExplodeLineByLineTopToDown,
        ExplodeLineByLineDownToTop,
        ExplodeLineByLineLeftToRight,
        ExplodeLineByLineRightToLeft,
        ExplodeOneByOneFromBottomLeft,
        ExplodeOneByOneFromBottomRight,
        ExplodeOneByOneFromTopLeft,
        ExplodeOneByOneFromTopRight,
        ExplodeOneByOneFromBottomLeftZigZag,
        ExplodeOneByOneFromBottomRightZigZag,
        ExplodeOneByOneFromTopLeftZigZag,
        ExplodeOneByOneFromTopRightZigZag
    }
    public enum ShuffleRefill { GoToCenterThenShuffle, ShuffleAllAtOnce }
    #endregion

    #region Private Variables
    private SoundManager soundMan;

    private int[,] _elementsMap;
    private GameObject[,] _elementsReferences;
    private List<GameObject> _elementsMovingReferences;
    private List<GameObject> _cascadeRefillReferences;

    private Vector2 _lastTileMoved;
    private TileScript.Direction _lastTileMovedMovement;

    public enum PowerUpDestroyType { Horizontal, Vertical }

    private Vector2[,] _coordinates;

    private bool _tilesAreMoving;
    private bool _isUndoing;
    private bool _cascadeIsInPlace;
    private bool _refillCascadeIsInPlace;
    private bool _noMoreMoves;
    private bool _shufflePerformed;
    private bool _isShuffleRearrangingInPlace;
    private bool _areTilesIntheMiddle;
    private bool _cantShuffle;
    private bool _started;
    private bool _areCollidersEnabled;

    private bool _isHintInPlace;
    private GameObject _currentHintedTile;
    private float _hintTimer;

    private float _refillTimer;
    private float _nextRefillExplosionTimer;
    private float _shuffleTimer;
    #endregion

    #endregion

    #region MonoBehaviour Inherited Functions

    private void OnEnable()
    {
        soundMan = GetComponent<SoundManager>();

        //You Can't Match 3 with less than 3, right?
        width = height < 3 ? 3 : width;
        height = height < 3 ? 3 : height;


        //Dynamic scaling based on the scale of the grid
        xSeparationBetweenElements *= transform.localScale.x;
        ySeparationBetweenElements *= transform.localScale.y;




        //Score is reset here and not in the OnDisable() because someone could use the variable when the game is finished
        score = 0;

        //You need at least 2 elements to be able to play, otherwise nothing happens
        if (elements.Length > 1)
        {
            _elementsMap = new int[width, height];
            _elementsReferences = new GameObject[width, height];
            _elementsMovingReferences = new List<GameObject>();
            _cascadeRefillReferences = new List<GameObject>();
            _coordinates = new Vector2[width, height];

            PopulateGrid();
            CheckMatchesAtStart();

            while (!AvailableMoves())
            {
                foreach (var item in _elementsReferences)
                    Destroy(item);

                PopulateGrid();
                CheckMatchesAtStart();
            }

            EnableColliders(true);
        }
        else
        {
            print("not Enough Elements");
        }
    }
    private void OnDisable()
    {
        //Resets specific variables to be able to replay without much hassle.

        xSeparationBetweenElements /= transform.localScale.x;
        ySeparationBetweenElements /= transform.localScale.y;

        if (_elementsReferences != null)
        {
            foreach (var item in _elementsReferences)
                if (item != null)
                    Destroy(item);
        }

        _elementsMap = null;
        _elementsReferences = null;
        _coordinates = null;
        _elementsMovingReferences = null;
        _cascadeRefillReferences = null;
        timeSinceStarted = 0f;
        _hintTimer = 0;
        _started = false;
        timeOut = false;
    }
    private void Update()
    {
        if (hintsDelayInMS < 1000f)
            hintsDelayInMS = 1000f;

        //Hints are triggered in this block
        #region Hints
        if (areHintsEnabled)
            if (!_isHintInPlace && !_tilesAreMoving)
                if (_hintTimer < hintsDelayInMS)
                    _hintTimer += Time.deltaTime * 1000;
                else
                    ActivateHint();
        #endregion


        //Timer is managed from this block
        #region Time
        if (!timeOut && isTimeEnabled)
            if (timerStartAtFirstMovement)
            {
                if (_started)
                    timeSinceStarted += Time.deltaTime;
                timeLeft = (timeLimit - timeSinceStarted) >= 0 ? (timeLimit - timeSinceStarted) : 0;
                if (timeLeft <= 0)
                    timeOut = true;
            }
            else
            {
                timeSinceStarted += Time.deltaTime;
                timeLeft = (timeLimit - timeSinceStarted) >= 0 ? (timeLimit - timeSinceStarted) : 0;
                if (timeLeft <= 0)
                    timeOut = true;
            }
        else if (isTimeEnabled && timeOut && _areCollidersEnabled)
            EnableColliders(false);
        #endregion

        //Moves ara Managed from This Block
        #region userTriggeredMove
        if (_tilesAreMoving || _isUndoing)
        {
            TileScript tileOnForeGround;
            TileScript tileOnBackGround;

            switch (_lastTileMovedMovement)
            {
                case TileScript.Direction.Up:
                    tileOnForeGround = _elementsReferences[(int)_lastTileMoved.x, (int)_lastTileMoved.y].GetComponent<TileScript>();
                    tileOnBackGround = _elementsReferences[(int)_lastTileMoved.x, (int)_lastTileMoved.y + 1].GetComponent<TileScript>();

                    if (!tileOnForeGround.IsMoving && !tileOnBackGround.IsMoving)
                    {
                        tileOnForeGround.Coordinates.y++;
                        tileOnBackGround.Coordinates.y--;

                        _elementsReferences[(int)tileOnForeGround.Coordinates.x, (int)tileOnForeGround.Coordinates.y] = tileOnForeGround.gameObject;
                        _elementsReferences[(int)tileOnBackGround.Coordinates.x, (int)tileOnBackGround.Coordinates.y] = tileOnBackGround.gameObject;

                        _elementsMap[(int)tileOnForeGround.Coordinates.x, (int)tileOnForeGround.Coordinates.y] = tileOnForeGround.TypeOfTile;
                        _elementsMap[(int)tileOnBackGround.Coordinates.x, (int)tileOnBackGround.Coordinates.y] = tileOnBackGround.TypeOfTile;

                        if (_tilesAreMoving)
                        {
                            _tilesAreMoving = false;
                            CheckMove();
                        }
                        else if (_isUndoing)
                        {
                            _isUndoing = false;
                            EnableColliders(true);
                        }
                    }
                    break;
                case TileScript.Direction.Down:
                    tileOnForeGround = _elementsReferences[(int)_lastTileMoved.x, (int)_lastTileMoved.y].GetComponent<TileScript>();
                    tileOnBackGround = _elementsReferences[(int)_lastTileMoved.x, (int)_lastTileMoved.y - 1].GetComponent<TileScript>();

                    if (!tileOnForeGround.IsMoving && !tileOnBackGround.IsMoving)
                    {
                        tileOnForeGround.Coordinates.y--;
                        tileOnBackGround.Coordinates.y++;
                        _elementsReferences[(int)tileOnForeGround.Coordinates.x, (int)tileOnForeGround.Coordinates.y] = tileOnForeGround.gameObject;
                        _elementsReferences[(int)tileOnBackGround.Coordinates.x, (int)tileOnBackGround.Coordinates.y] = tileOnBackGround.gameObject;
                        _elementsMap[(int)tileOnForeGround.Coordinates.x, (int)tileOnForeGround.Coordinates.y] = tileOnForeGround.TypeOfTile;
                        _elementsMap[(int)tileOnBackGround.Coordinates.x, (int)tileOnBackGround.Coordinates.y] = tileOnBackGround.TypeOfTile;

                        if (_tilesAreMoving)
                        {
                            _tilesAreMoving = false;
                            CheckMove();
                        }
                        else if (_isUndoing)
                        {
                            _isUndoing = false;
                            EnableColliders(true);
                        }
                    }
                    break;
                case TileScript.Direction.Left:
                    tileOnForeGround = _elementsReferences[(int)_lastTileMoved.x, (int)_lastTileMoved.y].GetComponent<TileScript>();
                    tileOnBackGround = _elementsReferences[(int)_lastTileMoved.x - 1, (int)_lastTileMoved.y].GetComponent<TileScript>();

                    if (!tileOnForeGround.IsMoving && !tileOnBackGround.IsMoving)
                    {
                        tileOnForeGround.Coordinates.x--;
                        tileOnBackGround.Coordinates.x++;
                        _elementsReferences[(int)tileOnForeGround.Coordinates.x, (int)tileOnForeGround.Coordinates.y] = tileOnForeGround.gameObject;
                        _elementsReferences[(int)tileOnBackGround.Coordinates.x, (int)tileOnBackGround.Coordinates.y] = tileOnBackGround.gameObject;
                        _elementsMap[(int)tileOnForeGround.Coordinates.x, (int)tileOnForeGround.Coordinates.y] = tileOnForeGround.TypeOfTile;
                        _elementsMap[(int)tileOnBackGround.Coordinates.x, (int)tileOnBackGround.Coordinates.y] = tileOnBackGround.TypeOfTile;

                        if (_tilesAreMoving)
                        {
                            _tilesAreMoving = false;
                            CheckMove();
                        }
                        else if (_isUndoing)
                        {
                            _isUndoing = false;
                            EnableColliders(true);
                        }
                    }
                    break;
                case TileScript.Direction.Right:
                    tileOnForeGround = _elementsReferences[(int)_lastTileMoved.x, (int)_lastTileMoved.y].GetComponent<TileScript>();
                    tileOnBackGround = _elementsReferences[(int)_lastTileMoved.x + 1, (int)_lastTileMoved.y].GetComponent<TileScript>();

                    if (!tileOnForeGround.IsMoving && !tileOnBackGround.IsMoving)
                    {
                        tileOnForeGround.Coordinates.x++;
                        tileOnBackGround.Coordinates.x--;
                        _elementsReferences[(int)tileOnForeGround.Coordinates.x, (int)tileOnForeGround.Coordinates.y] = tileOnForeGround.gameObject;
                        _elementsReferences[(int)tileOnBackGround.Coordinates.x, (int)tileOnBackGround.Coordinates.y] = tileOnBackGround.gameObject;
                        _elementsMap[(int)tileOnForeGround.Coordinates.x, (int)tileOnForeGround.Coordinates.y] = tileOnForeGround.TypeOfTile;
                        _elementsMap[(int)tileOnBackGround.Coordinates.x, (int)tileOnBackGround.Coordinates.y] = tileOnBackGround.TypeOfTile;

                        if (_tilesAreMoving)
                        {
                            _tilesAreMoving = false;
                            CheckMove();
                        }
                        else if (_isUndoing)
                        {
                            _isUndoing = false;
                            EnableColliders(true);
                        }
                    }
                    break;
                default:
                    break;
            }
        }
        #endregion

        //Cascade (when items are matched) is managed from this block;
        #region Cascade
        if (_cascadeIsInPlace)
        {
            _cascadeIsInPlace = false;

            foreach (var item in _elementsMovingReferences)
                if (item.GetComponent<TileScript>().IsMoving)
                {
                    _cascadeIsInPlace = true;
                    break;
                }

            if (!_cascadeIsInPlace)
            {
                _elementsMovingReferences.Clear();
                CheckMove();
            }
        }
        if (_refillCascadeIsInPlace)
        {
            _refillCascadeIsInPlace = false;

            foreach (var item in _elementsMovingReferences)
                if (item.GetComponent<TileScript>().IsMoving)
                {
                    _refillCascadeIsInPlace = true;
                    break;
                }

            if (!_refillCascadeIsInPlace)
            {
                _elementsMovingReferences.Clear();
                EnableColliders(true);
            }
        }
        #endregion

        //When no more moves are found, this block manages the flow of the refill or shuffle
        #region OnNoMoreMoves
        if (_noMoreMoves)
        {
            switch (whenNoMovesAvailable)
            {
                case RefillType.deleteAllAndCascade:
                    RefillScript();
                    break;
                case RefillType.Shuffle:
                    ShuffleScript();
                    break;
            }
        }
        #endregion
    }
    #endregion

    #region Public Functions
    /// <summary>
    /// Function to swap adjacent tiles positions
    /// </summary>
    /// <param name="tileCoor">Indicates the coordinate where the tile to move is located</param>
    /// <param name="direction">Indicates to where the tile has to move</param>
    /// <param name="undo">indicates if the movement is an undoing of a failed move, this param is false by default</param>
    public void MoveTile(Vector2 tileCoor, TileScript.Direction direction, bool undo = false)
    {
        if (areHintsEnabled)
            if (_isHintInPlace)
                DeactivateHint();
        _hintTimer = 0;

        switch (direction)
        {
            case TileScript.Direction.Up:
                if (tileCoor.y < height - 1)
                {
                    TileScript tileOnForeGround = _elementsReferences[(int)tileCoor.x, (int)tileCoor.y].GetComponent<TileScript>();
                    TileScript tileOnBackGround = _elementsReferences[(int)tileCoor.x, (int)tileCoor.y + 1].GetComponent<TileScript>();

                    var fgCoordinate = _coordinates[(int)tileCoor.x, (int)tileCoor.y + 1] + TransformV2();
                    var bgCoordinate = _coordinates[(int)tileCoor.x, (int)tileCoor.y] + TransformV2();

                    tileOnForeGround.MoveTo(fgCoordinate, tileMoveTimeDuration);
                    tileOnBackGround.MoveTo(bgCoordinate, tileMoveTimeDuration);

                    if (undo)
                    {
                        _isUndoing = true;
                        tileOnForeGround.MoveToBackground();
                    }
                    else
                    {
                        _tilesAreMoving = true;
                        tileOnBackGround.MoveToBackground();
                    }
                    _lastTileMoved = tileCoor;
                    _lastTileMovedMovement = direction;
                }
                break;
            case TileScript.Direction.Down:
                if (tileCoor.y > 0)
                {
                    TileScript tileOnForeGround = _elementsReferences[(int)tileCoor.x, (int)tileCoor.y].GetComponent<TileScript>();
                    TileScript tileOnBackGround = _elementsReferences[(int)tileCoor.x, (int)tileCoor.y - 1].GetComponent<TileScript>();

                    var fgCoordinate = _coordinates[(int)tileCoor.x, (int)tileCoor.y - 1] + TransformV2();
                    var bgCoordinate = _coordinates[(int)tileCoor.x, (int)tileCoor.y] + TransformV2();

                    tileOnForeGround.MoveTo(fgCoordinate, tileMoveTimeDuration);
                    tileOnBackGround.MoveTo(bgCoordinate, tileMoveTimeDuration);

                    if (undo)
                    {
                        _isUndoing = true;
                        tileOnForeGround.MoveToBackground();
                    }
                    else
                    {
                        _tilesAreMoving = true;
                        tileOnBackGround.MoveToBackground();
                    }
                    _lastTileMoved = tileCoor;
                    _lastTileMovedMovement = direction;
                }
                break;
            case TileScript.Direction.Left:
                if (tileCoor.x > 0)
                {
                    TileScript tileOnForeGround = _elementsReferences[(int)tileCoor.x, (int)tileCoor.y].GetComponent<TileScript>();
                    TileScript tileOnBackGround = _elementsReferences[(int)tileCoor.x - 1, (int)tileCoor.y].GetComponent<TileScript>();

                    var fgCoordinate = _coordinates[(int)tileCoor.x - 1, (int)tileCoor.y] + TransformV2();
                    var bgCoordinate = _coordinates[(int)tileCoor.x, (int)tileCoor.y] + TransformV2();

                    tileOnForeGround.MoveTo(fgCoordinate, tileMoveTimeDuration);
                    tileOnBackGround.MoveTo(bgCoordinate, tileMoveTimeDuration);

                    if (undo)
                    {
                        _isUndoing = true;
                        tileOnForeGround.MoveToBackground();
                    }
                    else
                    {
                        _tilesAreMoving = true;
                        tileOnBackGround.MoveToBackground();
                    }
                    _lastTileMoved = tileCoor;
                    _lastTileMovedMovement = direction;
                }
                break;
            case TileScript.Direction.Right:
                if (tileCoor.x < width - 1)
                {
                    TileScript tileOnForeGround = _elementsReferences[(int)tileCoor.x, (int)tileCoor.y].GetComponent<TileScript>();
                    TileScript tileOnBackGround = _elementsReferences[(int)tileCoor.x + 1, (int)tileCoor.y].GetComponent<TileScript>();

                    var fgCoordinate = _coordinates[(int)tileCoor.x + 1, (int)tileCoor.y] + TransformV2();
                    var bgCoordinate = _coordinates[(int)tileCoor.x, (int)tileCoor.y] + TransformV2();

                    tileOnForeGround.MoveTo(fgCoordinate, tileMoveTimeDuration);
                    tileOnBackGround.MoveTo(bgCoordinate, tileMoveTimeDuration);

                    if (undo)
                    {
                        _isUndoing = true;
                        tileOnForeGround.MoveToBackground();
                    }
                    else
                    {
                        _tilesAreMoving = true;
                        tileOnBackGround.MoveToBackground();
                    }
                    _lastTileMoved = tileCoor;
                    _lastTileMovedMovement = direction;
                }
                break;
            default:
                break;
        }
        if (_tilesAreMoving || _isUndoing)
            EnableColliders(false);
        if (!_started)
            _started = true;
    }
    #endregion

    #region Private Functions
    /// <summary>
    /// Function to return the position of the grid without the z axis...
    /// </summary>
    /// <returns></returns>
    private Vector2 TransformV2()
    {
        return new Vector2(transform.position.x, transform.position.y);
    }
    /// <summary>
    /// Function to enable or disable the boxColliders2D of the tile gameobjects so the player can or can not move the tiles
    /// </summary>
    /// <param name="enable">self explanatory</param>
    private void EnableColliders(bool enable)
    {
        foreach (var item in _elementsReferences)
            item.GetComponent<BoxCollider2D>().enabled = enable;
        _areCollidersEnabled = enable;
    }
    /// <summary>
    /// You can't match 3 if you don't have 3 equal tiles, this function returns if you have at least 3 equal tiles, used for shuffle.
    /// </summary>
    /// <returns>true if 3 equal tiles are found, false if not.</returns>
    private bool CheckForAtLeastThreeOfAKind()
    {
        int[] tilesCount = new int[elements.Length];

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                tilesCount[_elementsMap[x, y]]++;

        foreach (int count in tilesCount)
            if (count >= 3)
                return true;

        return false;
    }
    /// <summary>
    /// Returns a list with all the tiles where a match 3 is posible
    /// </summary>
    /// <returns>A list with all the tiles where a match 3 is posible</returns>
    private List<Vector2> GetAllAvailableMoves()
    {
        List<Vector2> listOfMoves = new List<Vector2>();
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                Vector2 coords = new Vector2(x, y);
                /*
                x z
                 y
                */
                if/*y*/((x > 0 && x < width - 1 && y < height - 1) && (_elementsMap[x - 1, y + 1] == _elementsMap[x, y]) && (_elementsMap[x + 1, y + 1] == _elementsMap[x, y]))
                    listOfMoves.Add(coords);

                /*
                 y
                x z
                */
                if /*y*/ ((x > 0 && x < width - 1 && y > 0) && (_elementsMap[x - 1, y - 1] == _elementsMap[x, y]) && (_elementsMap[x + 1, y - 1] == _elementsMap[x, y]))
                    listOfMoves.Add(coords);

                /*
                x
                 y
                z
                */
                if /*y*/((x > 0 && y < height - 1 && y > 0) && (_elementsMap[x - 1, y + 1] == _elementsMap[x, y]) && (_elementsMap[x - 1, y - 1] == _elementsMap[x, y]))
                    listOfMoves.Add(coords);

                /*
                 x
                y
                 z
                */
                if /*y*/((x < width - 1 && y < height - 1 && y > 0) && (_elementsMap[x + 1, y + 1] == _elementsMap[x, y]) && (_elementsMap[x + 1, y - 1] == _elementsMap[x, y]))
                    listOfMoves.Add(coords);

                /*
                xy
                  z
                */
                if /*z*/((x > 1 && y < height - 1) && (_elementsMap[x - 2, y + 1] == _elementsMap[x, y]) && (_elementsMap[x - 1, y + 1] == _elementsMap[x, y]))
                    listOfMoves.Add(coords);

                /*
                  z
                xy
                */
                if /*z*/((x > 1 && y > 0) && (_elementsMap[x - 2, y - 1] == _elementsMap[x, y]) && (_elementsMap[x - 1, y - 1] == _elementsMap[x, y]))
                    listOfMoves.Add(coords);

                /*
                 yz
                x
                */
                if /*x*/((x < width - 2 && y < height - 1) && (_elementsMap[x + 2, y + 1] == _elementsMap[x, y]) && (_elementsMap[x + 1, y + 1] == _elementsMap[x, y]))
                    listOfMoves.Add(coords);

                /*
                x
                 yz
                */
                if /*x*/((x < width - 2 && y > 0) && (_elementsMap[x + 1, y - 1] == _elementsMap[x, y]) && (_elementsMap[x + 2, y - 1] == _elementsMap[x, y]))
                    listOfMoves.Add(coords);

                /*
                 x
                y
                z
                */
                if /*x*/((x > 0 && y > 1) && (_elementsMap[x - 1, y - 1] == _elementsMap[x, y]) && (_elementsMap[x - 1, y - 2] == _elementsMap[x, y]))
                    listOfMoves.Add(coords);

                /*
                x
                 y
                 z
                */
                if /*x*/((x < width - 1 && y > 1) && (_elementsMap[x + 1, y - 1] == _elementsMap[x, y]) && (_elementsMap[x + 1, y - 2] == _elementsMap[x, y]))
                    listOfMoves.Add(coords);

                /*
                x
                y
                 z
                */
                if /*z*/((x > 0 && y < height - 2) && (_elementsMap[x - 1, y + 2] == _elementsMap[x, y]) && (_elementsMap[x - 1, y + 1] == _elementsMap[x, y]))
                    listOfMoves.Add(coords);

                /*
                 x
                 y
                z
                */
                if /*z*/((x < width - 1 && y < height - 2) && (_elementsMap[x + 1, y + 2] == _elementsMap[x, y]) && (_elementsMap[x + 1, y + 1] == _elementsMap[x, y]))
                    listOfMoves.Add(coords);

                /*
                xy z
                */
                if /*z*/((x > 2) && (_elementsMap[x - 2, y] == _elementsMap[x, y]) && (_elementsMap[x - 3, y] == _elementsMap[x, y]))
                    listOfMoves.Add(coords);

                /*
                x yz
                */
                if /*x*/((x < width - 3) && (_elementsMap[x + 2, y] == _elementsMap[x, y]) && (_elementsMap[x + 3, y] == _elementsMap[x, y]))
                    listOfMoves.Add(coords);

                /*
                x

                y
                z
                */
                if /*x*/((y > 2) && (_elementsMap[x, y - 3] == _elementsMap[x, y]) && (_elementsMap[x, y - 2] == _elementsMap[x, y]))
                    listOfMoves.Add(coords);

                /*
                x
                y

                z
                */
                if
                   /*z*/((y < height - 3) && (_elementsMap[x, y + 2] == _elementsMap[x, y]) && (_elementsMap[x, y + 3] == _elementsMap[x, y]))
                    listOfMoves.Add(coords);
            }
        return listOfMoves;
    }
    /// <summary>
    /// Checks the grid for matches
    /// </summary>
    private void CheckMove()
    {
        List<Vector2> matches = new List<Vector2>();
        List<Vector2> powerUp = new List<Vector2>();

        List<int> horizontalDestroy = new List<int>();
        List<int> verticalDestroy = new List<int>();

        Vector2 movedTileUpdatedPos = _lastTileMoved;
        if (scoreMultiplier == 0)
            switch (_lastTileMovedMovement)
            {
                case TileScript.Direction.Up:
                    movedTileUpdatedPos.y++;
                    break;
                case TileScript.Direction.Down:
                    movedTileUpdatedPos.y--;
                    break;
                case TileScript.Direction.Left:
                    movedTileUpdatedPos.x--;
                    break;
                case TileScript.Direction.Right:
                    movedTileUpdatedPos.x++;
                    break;
                default:
                    break;
            }

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (x > 0 && x < width - 1)
                {
                    if ((_elementsMap[x - 1, y] == _elementsMap[x, y] && _elementsMap[x, y] == _elementsMap[x + 1, y] && _elementsMap[x, y] != -1))
                    {
                        if (_elementsReferences[x - 1, y].GetComponent<TileScript>().HasPower) { if (!horizontalDestroy.Contains(y)) horizontalDestroy.Add(y); }
                        else if (_elementsReferences[x, y].GetComponent<TileScript>().HasPower) { if (!horizontalDestroy.Contains(y)) horizontalDestroy.Add(y); }
                        else if (_elementsReferences[x + 1, y].GetComponent<TileScript>().HasPower) { if (!horizontalDestroy.Contains(y)) horizontalDestroy.Add(y); }
                        else
                        {
                            if (!matches.Contains(new Vector2(x - 1, y)))
                                matches.Add(new Vector2(x - 1, y));
                            if (!matches.Contains(new Vector2(x, y)))
                                matches.Add(new Vector2(x, y));
                            if (!matches.Contains(new Vector2(x + 1, y)))
                                matches.Add(new Vector2(x + 1, y));


                            if (scoreMultiplier == 0 && arePowersEnabled)
                            {
                                if (x == (int)_lastTileMoved.x && y == (int)_lastTileMoved.y)
                                    if ((x > 1 && _elementsMap[x - 2, y] == _elementsMap[x, y]) || (x < width - 2 && _elementsMap[x + 2, y] == _elementsMap[x, y]))
                                        if (!powerUp.Contains(new Vector2(x, y)))
                                            powerUp.Add(new Vector2(x, y));

                                if (x == (int)movedTileUpdatedPos.x && y == (int)movedTileUpdatedPos.y)
                                    if ((x > 1 && _elementsMap[x - 2, y] == _elementsMap[x, y]) || (x < width - 2 && _elementsMap[x + 2, y] == _elementsMap[x, y]))
                                        if (!powerUp.Contains(new Vector2(x, y)))
                                            powerUp.Add(new Vector2(x, y));
                            }
                        }
                    }
                }

                if (y > 0 && y < height - 1)
                {
                    if ((_elementsMap[x, y - 1] == _elementsMap[x, y] && _elementsMap[x, y] == _elementsMap[x, y + 1] && _elementsMap[x, y] != -1))
                    {
                        if (_elementsReferences[x, y - 1].GetComponent<TileScript>().HasPower) { if (!verticalDestroy.Contains(x)) verticalDestroy.Add(x); }
                        else if (_elementsReferences[x, y].GetComponent<TileScript>().HasPower) { if (!verticalDestroy.Contains(x)) verticalDestroy.Add(x); }
                        else if (_elementsReferences[x, y + 1].GetComponent<TileScript>().HasPower) { if (!verticalDestroy.Contains(x)) verticalDestroy.Add(x); }
                        else
                        {
                            if (!matches.Contains(new Vector2(x, y - 1)))
                                matches.Add(new Vector2(x, y - 1));
                            if (!matches.Contains(new Vector2(x, y)))
                                matches.Add(new Vector2(x, y));
                            if (!matches.Contains(new Vector2(x, y + 1)))
                                matches.Add(new Vector2(x, y + 1));

                            if (scoreMultiplier == 0 && arePowersEnabled)
                            {
                                if (x == _lastTileMoved.x && y == _lastTileMoved.y)
                                    if ((y > 1 && _elementsMap[x, y - 2] == _elementsMap[x, y]) || (y < height - 2 && _elementsMap[x, y + 2] == _elementsMap[x, y]))
                                        if (!powerUp.Contains(new Vector2(x, y)))
                                            powerUp.Add(new Vector2(x, y));

                                if (x == movedTileUpdatedPos.x && y == movedTileUpdatedPos.y)
                                    if ((y > 1 && _elementsMap[x, y - 2] == _elementsMap[x, y]) || (y < height - 2 && _elementsMap[x, y + 2] == _elementsMap[x, y]))
                                        if (!powerUp.Contains(new Vector2(x, y)))
                                            powerUp.Add(new Vector2(x, y));
                            }
                        }
                    }
                }
            }
        foreach (var item in matches)
        {
            bool isPowerUp = false;
            foreach (var item2 in powerUp)
                if (item2.x == item.x && item2.y == item.y)
                {
                    _elementsReferences[(int)item.x, (int)item.y].GetComponent<TileScript>().EnablePowerUp();
                    isPowerUp = true;
                    break;
                }
            if (isPowerUp)
                continue;

            _elementsReferences[(int)item.x, (int)item.y].GetComponent<TileScript>().TriggerExplosion();
            _elementsMap[(int)item.x, (int)item.y] = -1;
        }

        for (int x = 0; x < width; x++)
            foreach (var y in horizontalDestroy)
                if (_elementsReferences[x, y] != null)
                {
                    _elementsReferences[x, y].GetComponent<TileScript>().TriggerExplosion();
                    _elementsMap[x, y] = -1;
                    matches.Add(new Vector2());
                }

        for (int y = 0; y < height; y++)
            foreach (var x in verticalDestroy)
                if (_elementsReferences[x, y] != null)
                {
                    _elementsReferences[x, y].GetComponent<TileScript>().TriggerExplosion();
                    _elementsMap[x, y] = -1;
                    matches.Add(new Vector2());
                }

        if (matches.Count > 0)
        {
            soundMan.PlaySound(0, 1 + (0.25f * scoreMultiplier));
            scoreMultiplier++;
            score += (matches.Count * scorePerTile * scoreMultiplier);
            PopulateFromTop();
            _lastTileMovedMovement = TileScript.Direction.None;
        }
        else
        {
            if (scoreMultiplier == 0)
                UndoMoveTile();
            else
            {
                if (AvailableMoves())
                    EnableColliders(true);
                else
                {
                    _noMoreMoves = true;
                }
            }
            scoreMultiplier = 0;
        }
    }
    /// <summary>
    /// Undoes the last movement
    /// </summary>
    private void UndoMoveTile()
    {
        MoveTile(_lastTileMoved, _lastTileMovedMovement, undo: true);
        _isUndoing = true;
    }
    /// <summary>
    /// Checks if the player can perform a move and match 3
    /// </summary>
    /// <returns>True if tiles can be matched, false if not</returns>
    private bool AvailableMoves()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                /*
                x z
                 y
                */
                if /*x*/(((x < width - 2 && y > 0) && (_elementsMap[x + 1, y - 1] == _elementsMap[x, y]) && (_elementsMap[x + 2, y] == _elementsMap[x, y])) ||
                   /*y*/((x > 0 && x < width - 1 && y < height - 1) && (_elementsMap[x - 1, y + 1] == _elementsMap[x, y]) && (_elementsMap[x + 1, y + 1] == _elementsMap[x, y])) ||
                   /*z*/((x > 1 && y > 0) && (_elementsMap[x - 2, y] == _elementsMap[x, y]) && (_elementsMap[x - 1, y - 1] == _elementsMap[x, y])))
                    return true;

                /*
                 y
                x z
                */
                if /*x*/(((x < width - 2 && y < height - 2) && _elementsMap[x + 1, y + 1] == _elementsMap[x, y] && _elementsMap[x + 2, y + 1] == _elementsMap[x, y]) ||
                   /*y*/ ((x > 0 && x < width - 1 && y > 0) && (_elementsMap[x - 1, y - 1] == _elementsMap[x, y]) && (_elementsMap[x + 1, y - 1] == _elementsMap[x, y])) ||
                   /*z*/ ((x > 1 && y < height - 2) && _elementsMap[x - 2, y + 1] == _elementsMap[x, y] && _elementsMap[x - 1, y + 1] == _elementsMap[x, y]))
                    return true;

                /*
                x
                 y
                z
                */
                if /*x*/(((x < width - 2 && y > 1) && _elementsMap[x + 1, y - 1] == _elementsMap[x, y] && _elementsMap[x, y - 2] == _elementsMap[x, y]) ||
                   /*y*/((x > 0 && y < height - 1 && y > 0) && (_elementsMap[x - 1, y + 1] == _elementsMap[x, y]) && (_elementsMap[x - 1, y - 1] == _elementsMap[x, y])) ||
                   /*z*/((x < width - 2 && y < height - 2) && _elementsMap[x, y + 2] == _elementsMap[x, y] && _elementsMap[x + 1, y + 1] == _elementsMap[x, y]))
                    return true;

                /*
                 x
                y
                 z
                */
                if /*x*/(((x > 0 && y > 2) && _elementsMap[x - 1, y - 1] == _elementsMap[x, y] && _elementsMap[x, y - 2] == _elementsMap[x, y]) ||
                   /*y*/((x < width - 1 && y < height - 1 && y > 0) && (_elementsMap[x + 1, y + 1] == _elementsMap[x, y]) && (_elementsMap[x + 1, y - 1] == _elementsMap[x, y])) ||
                   /*z*/((x > 0 && y < height - 2) && _elementsMap[x, y] == _elementsMap[x - 1, y + 1] && _elementsMap[x, y + 2] == _elementsMap[x, y]))
                    return true;

                /*
                xy
                  z
                */
                if /*x*/(((x < width - 2 && y > 0) && _elementsMap[x + 1, y] == _elementsMap[x, y] && _elementsMap[x + 2, y - 1] == _elementsMap[x, y]) ||
                   /*y*/((x > 0 && x < width - 1 && y > 0) && _elementsMap[x - 1, y] == _elementsMap[x, y] && _elementsMap[x + 1, y - 1] == _elementsMap[x, y]) ||
                   /*z*/((x > 1 && y < height - 1) && (_elementsMap[x - 2, y + 1] == _elementsMap[x, y]) && (_elementsMap[x - 1, y + 1] == _elementsMap[x, y])))
                    return true;

                /*
                  z
                xy
                */
                if /*x*/(((x < width - 2 && y < height - 1) && _elementsMap[x + 1, y] == _elementsMap[x, y] && _elementsMap[x + 2, y + 1] == _elementsMap[x, y]) ||
                   /*y*/((x > 0 && x < width - 1 && y < height - 1) && _elementsMap[x - 1, y] == _elementsMap[x, y] && _elementsMap[x + 1, y + 1] == _elementsMap[x, y]) ||
                   /*z*/((x > 1 && y > 0) && (_elementsMap[x - 2, y - 1] == _elementsMap[x, y]) && (_elementsMap[x - 1, y - 1] == _elementsMap[x, y])))
                    return true;

                /*
                 yz
                x
                */
                if /*x*/(((x < width - 2 && y < height - 1) && (_elementsMap[x + 2, y + 1] == _elementsMap[x, y]) && (_elementsMap[x + 1, y + 1] == _elementsMap[x, y])) ||
                   /*y*/((x > 0 && x < width - 1 && y > 0) && _elementsMap[x - 1, y - 1] == _elementsMap[x, y] && _elementsMap[x + 1, y] == _elementsMap[x, y]) ||
                   /*z*/((x > 1 && y > 0) && _elementsMap[x - 2, y - 1] == _elementsMap[x, y] && _elementsMap[x - 1, y] == _elementsMap[x, y]))
                    return true;

                /*
                x
                 yz
                */
                if /*x*/(((x < width - 2 && y > 0) && (_elementsMap[x + 1, y - 1] == _elementsMap[x, y]) && (_elementsMap[x + 2, y - 1] == _elementsMap[x, y])) ||
                   /*y*/((x > 0 && x < width - 1 && y < height - 1) && _elementsMap[x - 1, y + 1] == _elementsMap[x, y] && _elementsMap[x + 1, y] == _elementsMap[x, y]) ||
                   /*z*/((x > 1 && y < height - 1) && _elementsMap[x - 2, y + 1] == _elementsMap[x, y] && _elementsMap[x - 1, y] == _elementsMap[x, y]))
                    return true;

                /*
                 x
                y
                z
                */
                if /*x*/(((x > 0 && y > 1) && (_elementsMap[x - 1, y - 1] == _elementsMap[x, y]) && (_elementsMap[x - 1, y - 2] == _elementsMap[x, y])) ||
                   /*y*/((x < width - 1 && y > 0 && y < height - 1) && _elementsMap[x + 1, y + 1] == _elementsMap[x, y] && _elementsMap[x, y - 1] == _elementsMap[x, y]) ||
                   /*z*/((x < width - 1 && y < height - 2) && _elementsMap[x + 1, y + 2] == _elementsMap[x, y] && _elementsMap[x, y + 1] == _elementsMap[x, y]))
                    return true;

                /*
                x
                 y
                 z
                */
                if /*x*/(((x < width - 1 && y > 1) && (_elementsMap[x + 1, y - 1] == _elementsMap[x, y]) && (_elementsMap[x + 1, y - 2] == _elementsMap[x, y])) ||
                   /*y*/((x > 0 && y > 0 && y < height - 1) && _elementsMap[x - 1, y + 1] == _elementsMap[x, y] && _elementsMap[x, y - 1] == _elementsMap[x, y]) ||
                   /*z*/((x > 0 && y < height - 2) && _elementsMap[x - 1, y + 2] == _elementsMap[x, y] && _elementsMap[x - 1, y + 1] == _elementsMap[x, y]))
                    return true;

                /*
                x
                y
                 z
                */
                if /*x*/(((x < width - 1 && y > 1) && _elementsMap[x, y - 1] == _elementsMap[x, y] && _elementsMap[x + 1, y - 2] == _elementsMap[x, y]) ||
                   /*y*/((x < width - 1 && y > 0 && y < height - 1) && _elementsMap[x, y + 1] == _elementsMap[x, y] && _elementsMap[x + 1, y - 1] == _elementsMap[x, y]) ||
                   /*z*/((x > 0 && y < height - 2) && (_elementsMap[x - 1, y + 2] == _elementsMap[x, y]) && (_elementsMap[x - 1, y + 1] == _elementsMap[x, y])))
                    return true;

                /*
                 x
                 y
                z
                */
                if /*x*/(((x > 0 && y > 1) && _elementsMap[x, y - 1] == _elementsMap[x, y] && _elementsMap[x - 1, y - 2] == _elementsMap[x, y]) ||
                   /*y*/((x > 0 && y > 0 && y < height - 1) && _elementsMap[x, y + 1] == _elementsMap[x, y] && _elementsMap[x - 1, y - 1] == _elementsMap[x, y]) ||
                   /*z*/((x < width - 1 && y < height - 2) && (_elementsMap[x + 1, y + 2] == _elementsMap[x, y]) && (_elementsMap[x + 1, y + 1] == _elementsMap[x, y])))
                    return true;

                /*
                xy z
                */
                if /*x*/(((x < width - 3) && _elementsMap[x + 1, y] == _elementsMap[x, y] && _elementsMap[x + 3, y] == _elementsMap[x, y]) ||
                   /*y*/((x > 0 && x < width - 2) && _elementsMap[x - 1, y] == _elementsMap[x, y] && _elementsMap[x + 2, y] == _elementsMap[x, y]) ||
                   /*z*/((x > 2) && (_elementsMap[x - 2, y] == _elementsMap[x, y]) && (_elementsMap[x - 3, y] == _elementsMap[x, y])))
                    return true;

                /*
                x yz
                */
                if /*x*/(((x < width - 3) && (_elementsMap[x + 2, y] == _elementsMap[x, y]) && (_elementsMap[x + 3, y] == _elementsMap[x, y])) ||
                   /*y*/((x > 1 && x < width - 1) && _elementsMap[x - 2, y] == _elementsMap[x, y] && _elementsMap[x + 1, y] == _elementsMap[x, y]) ||
                   /*z*/((x > 2) && _elementsMap[x - 3, y] == _elementsMap[x, y] && _elementsMap[x - 1, y] == _elementsMap[x, y]))
                    return true;

                /*
                x

                y
                z
                */
                if /*x*/(((y > 2) && (_elementsMap[x, y - 3] == _elementsMap[x, y]) && (_elementsMap[x, y - 2] == _elementsMap[x, y])) ||
                   /*y*/((y > 0 && y < height - 2) && _elementsMap[x, y + 2] == _elementsMap[x, y] && _elementsMap[x, y - 1] == _elementsMap[x, y]) ||
                   /*z*/((y < height - 3) && _elementsMap[x, y + 1] == _elementsMap[x, y] && _elementsMap[x, y + 3] == _elementsMap[x, y]))
                    return true;

                /*
                x
                y

                z
                */
                if /*x*/(((y > 2) && _elementsMap[x, y - 1] == _elementsMap[x, y] && _elementsMap[x, y - 3] == _elementsMap[x, y]) ||
                   /*y*/((y > 1 && y < height - 3) && _elementsMap[x, y + 1] == _elementsMap[x, y] && _elementsMap[x, y - 2] == _elementsMap[x, y]) ||
                   /*z*/((y < height - 3) && (_elementsMap[x, y + 2] == _elementsMap[x, y]) && (_elementsMap[x, y + 3] == _elementsMap[x, y])))
                    return true;
            }
        }
        return false;
    }
    /// <summary>
    /// Replaces the missing elements of the grid, the new elements spawn from top
    /// </summary>
    private void PopulateFromTop()
    {
        for (int x = 0; x < width; x++)
        {
            int missingCount = 0;
            for (int y = 0; y < height; y++)
            {
                if (_elementsMap[x, y] == -1)
                    missingCount++;
                else if (missingCount > 0)
                {
                    _elementsReferences[x, y].GetComponent<TileScript>().MoveTo(_coordinates[x, y - missingCount] + TransformV2(), cascadeTimeDuration);
                    _elementsReferences[x, y].GetComponent<TileScript>().IsMoving = true;
                    _elementsReferences[x, y].GetComponent<TileScript>().Coordinates = new Vector2(x, y - missingCount);


                    _elementsReferences[x, y - missingCount] = _elementsReferences[x, y];
                    _elementsReferences[x, y] = null;

                    _elementsMap[x, y - missingCount] = _elementsMap[x, y];
                    _elementsMap[x, y] = -1;

                    _elementsMovingReferences.Add(_elementsReferences[x, y - missingCount]);
                    _cascadeIsInPlace = true;
                }
            }
            for (int i = 0; i < missingCount; i++)
            {
                int typeOfTile = Random.Range(0, elements.Length);

                Vector3 SpawnCoords = _coordinates[x, height - 1] + TransformV2();
                SpawnCoords.y += (ySeparationBetweenElements * (missingCount - (i)));

                GameObject go = Instantiate(elements[typeOfTile], SpawnCoords, Quaternion.Euler(Vector3.zero)) as GameObject;
                go.transform.SetParent(transform);
                go.transform.localScale = Vector3.one;
                go.GetComponent<TileScript>().MoveTo(_coordinates[x, (height - 1) - i] + TransformV2(), cascadeTimeDuration);
                go.GetComponent<TileScript>().IsMoving = true;
                go.GetComponent<TileScript>().ParentGrid = gameObject;
                go.GetComponent<TileScript>().Coordinates = new Vector2(x, (height - 1) - i);
                go.GetComponent<TileScript>().TypeOfTile = typeOfTile;

                _elementsMap[x, (height - 1) - i] = typeOfTile;
                _elementsReferences[x, (height - 1) - i] = go;

                _elementsMovingReferences.Add(_elementsReferences[x, (height - 1) - i]);
            }
        }
        _cascadeIsInPlace = true;
    }
    /// <summary>
    /// Destroys all elements and spawns fresh ones on top with posible matches available to the player.
    /// </summary>
    private void RepopulateGridWhenNoMoreMoves()
    {
        while (!AvailableMoves())
        {

            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    _elementsMap[x, y] = -1;
            foreach (var go in _elementsReferences)
                Destroy(go);

            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    int typeOfTile = Random.Range(0, elements.Length);

                    Vector2 SpawnCoords = _coordinates[x, y];
                    SpawnCoords.y += (_coordinates[x, height - 1] * 2f).y + ySeparationBetweenElements;
                    SpawnCoords += TransformV2();


                    GameObject go = Instantiate(elements[typeOfTile], SpawnCoords, Quaternion.Euler(Vector3.zero)) as GameObject;
                    go.transform.SetParent(transform);
                    go.transform.localScale = Vector3.one;
                    go.GetComponent<TileScript>().MoveTo(_coordinates[x, y] + TransformV2(), cascadeTimeDuration);
                    go.GetComponent<TileScript>().ParentGrid = gameObject;
                    go.GetComponent<TileScript>().IsMoving = true;
                    go.GetComponent<TileScript>().Coordinates = new Vector2(x, y);
                    go.GetComponent<TileScript>().TypeOfTile = typeOfTile;

                    _elementsMap[x, y] = typeOfTile;
                    _elementsReferences[x, y] = go;
                }
            CheckMatchesAfterRepopulation();
        }

        foreach (var go in _elementsReferences)
            _elementsMovingReferences.Add(go);


        _refillCascadeIsInPlace = true;
        _noMoreMoves = false;
    }
    /// <summary>
    /// checks the grid for matches, if matches are found a tile of the match is changed for a random one not being the same tile. at least a posible matche is always left.
    /// </summary>
    private void CheckMatchesAfterRepopulation()
    {
        bool hasMatches = false;
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (0 < x && x < width - 1)
                    if (_elementsMap[x - 1, y] == _elementsMap[x, y] && _elementsMap[x, y] == _elementsMap[x + 1, y] && _elementsMap[x, y] != -1)
                    {
                        int randIndex = Random.Range(-1, 1);
                        int typeOfTile = Random.Range(0, elements.Length);

                        Destroy(_elementsReferences[x + randIndex, y]);

                        GameObject go = Instantiate(elements[typeOfTile], (new Vector2(0, _coordinates[x, height - 1].y + ySeparationBetweenElements)) + _coordinates[x + randIndex, y] + TransformV2(), Quaternion.Euler(Vector3.zero)) as GameObject;
                        go.transform.SetParent(transform);
                        go.transform.localScale = Vector3.one;
                        go.GetComponent<TileScript>().MoveTo(_coordinates[x + randIndex, y] + TransformV2(), cascadeTimeDuration);
                        go.GetComponent<TileScript>().ParentGrid = gameObject;
                        go.GetComponent<TileScript>().IsMoving = true;
                        go.GetComponent<TileScript>().Coordinates = new Vector2(x + randIndex, y);
                        go.GetComponent<TileScript>().TypeOfTile = typeOfTile;

                        _elementsMap[x + randIndex, y] = typeOfTile;
                        _elementsReferences[x + randIndex, y] = go;
                        hasMatches = true;
                    }

                if (0 < y && y < height - 1)
                    if (_elementsMap[x, y - 1] == _elementsMap[x, y] && _elementsMap[x, y] == _elementsMap[x, y + 1])
                    {
                        int randIndex = Random.Range(-1, 1);
                        int typeOfTile = Random.Range(0, elements.Length);

                        Destroy(_elementsReferences[x, y + randIndex]);


                        GameObject go = Instantiate(elements[typeOfTile], (new Vector2(0, _coordinates[x, height - 1].y + ySeparationBetweenElements)) + _coordinates[x, y + randIndex] + TransformV2(), Quaternion.Euler(Vector3.zero)) as GameObject;
                        go.transform.SetParent(transform);
                        go.transform.localScale = Vector3.one;
                        go.GetComponent<TileScript>().MoveTo(_coordinates[x, y + randIndex] + TransformV2(), cascadeTimeDuration);
                        go.GetComponent<TileScript>().ParentGrid = gameObject;
                        go.GetComponent<TileScript>().IsMoving = true;
                        go.GetComponent<TileScript>().Coordinates = new Vector2(x, y + randIndex);
                        go.GetComponent<TileScript>().TypeOfTile = typeOfTile;

                        _elementsMap[x, y + randIndex] = typeOfTile;
                        _elementsReferences[x, y + randIndex] = go;
                        hasMatches = true;
                    }
            }
        if (hasMatches)
            CheckMatchesAfterRepopulation();
    }
    /// <summary>
    /// Shuffles the grid, self explanatory, if there are not 3 elements of the same kind, razes the grid and then the selected CascadeBehavior is performed
    /// </summary>
    private void Shuffle()
    {
        if (!CheckForAtLeastThreeOfAKind())
        {
            _cantShuffle = true;
            return;
        }
        while (!AvailableMoves())
        {
            List<GameObject> shuffleReferences = new List<GameObject>();
            foreach (var go in _elementsReferences)
                shuffleReferences.Add(go);

            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    int index = Random.Range(0, shuffleReferences.Count);
                    _elementsReferences[x, y] = shuffleReferences[index];
                    _elementsReferences[x, y].GetComponent<TileScript>().Coordinates = new Vector2(x, y);
                    _elementsMap[x, y] = _elementsReferences[x, y].GetComponent<TileScript>().TypeOfTile;
                    shuffleReferences.RemoveAt(index);
                }
        }
        CheckMatchesAfterShuffle();
        _shufflePerformed = true;
    }
    /// <summary>
    /// checks the grid for matches, if matches are found a tile of the match is shuffled for a random one not being the same tile. at least a posible matche is always left.
    /// </summary>
    private void CheckMatchesAfterShuffle()
    {

        bool hasMatches = false;
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (0 < x && x < width - 1)
                    if (_elementsMap[x - 1, y] == _elementsMap[x, y] && _elementsMap[x, y] == _elementsMap[x + 1, y] && _elementsMap[x, y] != -1)
                    {
                        int xNewPos = Random.Range(0, width);
                        int yNewPos = Random.Range(0, height);
                        int Rand = Random.Range(-1, 1);

                        while (xNewPos == x) xNewPos = Random.Range(0, width) + Rand;
                        while (yNewPos == y) yNewPos = Random.Range(0, height);

                        GameObject auxiliarReference = _elementsReferences[x, y];
                        int auxiliarInt = _elementsMap[x, y];

                        _elementsReferences[x, y] = _elementsReferences[xNewPos, yNewPos];
                        _elementsReferences[xNewPos, yNewPos] = auxiliarReference;

                        _elementsReferences[x, y].GetComponent<TileScript>().Coordinates = new Vector2(x, y);
                        _elementsReferences[xNewPos, yNewPos].GetComponent<TileScript>().Coordinates = new Vector2(xNewPos, yNewPos);

                        auxiliarReference = null;

                        _elementsMap[x, y] = _elementsMap[xNewPos, yNewPos];
                        _elementsMap[xNewPos, yNewPos] = auxiliarInt;

                        hasMatches = true;
                    }

                if (0 < y && y < height - 1)
                    if (_elementsMap[x, y - 1] == _elementsMap[x, y] && _elementsMap[x, y] == _elementsMap[x, y + 1])
                    {
                        int xNewPos = Random.Range(0, width);
                        int yNewPos = Random.Range(0, height);
                        int Rand = Random.Range(-1, 1);

                        while (xNewPos == x) xNewPos = Random.Range(0, width);
                        while (yNewPos == y) yNewPos = Random.Range(0, height) + Rand;

                        GameObject auxiliarReference = _elementsReferences[x, y];
                        int auxiliarInt = _elementsMap[x, y];

                        _elementsReferences[x, y] = _elementsReferences[xNewPos, yNewPos];
                        _elementsReferences[xNewPos, yNewPos] = auxiliarReference;

                        _elementsReferences[x, y].GetComponent<TileScript>().Coordinates = new Vector2(x, y);
                        _elementsReferences[xNewPos, yNewPos].GetComponent<TileScript>().Coordinates = new Vector2(xNewPos, yNewPos);

                        auxiliarReference = null;

                        _elementsMap[x, y] = _elementsMap[xNewPos, yNewPos];
                        _elementsMap[xNewPos, yNewPos] = auxiliarInt;

                        hasMatches = true;
                    }
            }
        if (hasMatches)
            CheckMatchesAfterShuffle();
        else if (!AvailableMoves())
            Shuffle();
    }
    /// <summary>
    /// Performs the first population of the grid
    /// </summary>
    private void PopulateGrid()
    {
        float xStartingPoint = -((xSeparationBetweenElements * (width / 2f)) - (xSeparationBetweenElements / 2f));
        float yStartingPoint = -((ySeparationBetweenElements * (height / 2f)) - (ySeparationBetweenElements / 2f));

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                _coordinates[x, y] = new Vector2(xStartingPoint + (x * xSeparationBetweenElements), yStartingPoint + (y * ySeparationBetweenElements));

                int typeOfTile = Random.Range(0, elements.Length);

                GameObject go = Instantiate(elements[typeOfTile], _coordinates[x, y] + TransformV2(), Quaternion.Euler(Vector3.zero)) as GameObject;
                go.transform.SetParent(transform);
                go.transform.localScale = Vector3.one;
                go.GetComponent<TileScript>().ParentGrid = gameObject;
                go.GetComponent<TileScript>().Coordinates = new Vector2(x, y);
                go.GetComponent<TileScript>().TypeOfTile = typeOfTile;

                _elementsMap[x, y] = typeOfTile;
                _elementsReferences[x, y] = go;
            }
    }
    /// <summary>
    /// checks the grid for matches, if matches are found a tile of the match is changed for a random one not being the same tile. at least a posible matche is always left.
    /// </summary>
    private void CheckMatchesAtStart()
    {
        bool hasMatches = false;
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (0 < x && x < width - 1)
                    if (_elementsMap[x - 1, y] == _elementsMap[x, y] && _elementsMap[x, y] == _elementsMap[x + 1, y] && _elementsMap[x, y] != -1)
                    {
                        int randIndex = Random.Range(-1, 1);
                        int typeOfTile = Random.Range(0, elements.Length);

                        Destroy(_elementsReferences[x + randIndex, y]);

                        GameObject go = Instantiate(elements[typeOfTile], _coordinates[x + randIndex, y] + TransformV2(), Quaternion.Euler(Vector3.zero)) as GameObject;
                        go.transform.SetParent(transform);
                        go.transform.localScale = Vector3.one;
                        go.GetComponent<TileScript>().ParentGrid = gameObject;
                        go.GetComponent<TileScript>().Coordinates = new Vector2(x + randIndex, y);
                        go.GetComponent<TileScript>().TypeOfTile = typeOfTile;

                        _elementsMap[x + randIndex, y] = typeOfTile;
                        _elementsReferences[x + randIndex, y] = go;
                        hasMatches = true;
                    }

                if (0 < y && y < height - 1)
                    if (_elementsMap[x, y - 1] == _elementsMap[x, y] && _elementsMap[x, y] == _elementsMap[x, y + 1])
                    {
                        int randIndex = Random.Range(-1, 1);
                        int typeOfTile = Random.Range(0, elements.Length);

                        Destroy(_elementsReferences[x, y + randIndex]);

                        GameObject go = Instantiate(elements[typeOfTile], _coordinates[x, y + randIndex] + TransformV2(), Quaternion.Euler(Vector3.zero)) as GameObject;
                        go.transform.SetParent(transform);
                        go.transform.localScale = Vector3.one;
                        go.GetComponent<TileScript>().ParentGrid = gameObject;
                        go.GetComponent<TileScript>().Coordinates = new Vector2(x, y + randIndex);
                        go.GetComponent<TileScript>().TypeOfTile = typeOfTile;

                        _elementsMap[x, y + randIndex] = typeOfTile;
                        _elementsReferences[x, y + randIndex] = go;
                        hasMatches = true;
                    }
            }
        if (hasMatches)
            CheckMatchesAtStart();
    }
    /// <summary>
    /// Performs the behavior selected con CascadeBehavior, MUST be called from Update() or FixedUpdate();
    /// </summary>
    private void RefillScript()
    {
        if (_refillTimer < refillGridDelayInMS)
            _refillTimer += Time.deltaTime * 1000;
        else
        {
            bool allDeleted = false;
            switch (CascadeBehavior)
            {
                #region Unsorted
                case CascadeRefill.ExplodeAllAtOnce:

                    for (int x = 0; x < width; x++)
                        for (int y = 0; y < height; y++)
                        {
                            _elementsReferences[x, y].GetComponent<TileScript>().TriggerExplosion();
                            _elementsReferences[x, y] = null;
                        }

                    RepopulateGridWhenNoMoreMoves();
                    break;
                case CascadeRefill.ExplodeAtRandom:
                    if (_cascadeRefillReferences.Count == 0)
                    {
                        foreach (var go in _elementsReferences)
                            _cascadeRefillReferences.Add(go);

                        for (int x = 0; x < width; x++)
                            for (int y = 0; y < height; y++)
                            {
                                _elementsReferences[x, y] = null;
                            }
                    }

                    _nextRefillExplosionTimer += Time.deltaTime * 1000;

                    if (_nextRefillExplosionTimer >= DelayBetweenExplosionsInMS)
                    {
                        var go = _cascadeRefillReferences[Random.Range(0, _cascadeRefillReferences.Count)];
                        go.GetComponent<TileScript>().TriggerExplosion();
                        _cascadeRefillReferences.Remove(go);
                        _nextRefillExplosionTimer = 0;
                    }

                    if (_cascadeRefillReferences.Count == 0)
                        allDeleted = true;
                    break;

                #endregion
                #region Line By Line
                case CascadeRefill.ExplodeLineByLineTopToDown:
                    _nextRefillExplosionTimer += Time.deltaTime * 1000;

                    if (_nextRefillExplosionTimer >= DelayBetweenExplosionsInMS)
                    {
                        _nextRefillExplosionTimer = 0;
                        bool deletedLine = false;
                        for (int y = height - 1; y >= 0; y--)
                        {
                            for (int x = width - 1; x >= 0; x--)
                            {
                                if (_elementsReferences[x, y] != null)
                                {
                                    _elementsReferences[x, y].GetComponent<TileScript>().TriggerExplosion();
                                    _elementsReferences[x, y] = null;
                                    if (x == 0)
                                        deletedLine = true;
                                }
                            }
                            if (deletedLine)
                                break;
                            if (y == 0)
                                allDeleted = true;
                        }
                    }
                    break;

                case CascadeRefill.ExplodeLineByLineDownToTop:
                    _nextRefillExplosionTimer += Time.deltaTime * 1000;

                    if (_nextRefillExplosionTimer >= DelayBetweenExplosionsInMS)
                    {
                        _nextRefillExplosionTimer = 0;
                        bool deletedLine = false;
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                if (_elementsReferences[x, y] != null)
                                {
                                    _elementsReferences[x, y].GetComponent<TileScript>().TriggerExplosion();
                                    _elementsReferences[x, y] = null;
                                    if (x == width - 1)
                                        deletedLine = true;
                                }
                            }
                            if (deletedLine)
                                break;
                            if (y == height - 1)
                                allDeleted = true;
                        }
                    }
                    break;

                case CascadeRefill.ExplodeLineByLineLeftToRight:
                    _nextRefillExplosionTimer += Time.deltaTime * 1000;

                    if (_nextRefillExplosionTimer >= DelayBetweenExplosionsInMS)
                    {
                        _nextRefillExplosionTimer = 0;
                        bool deletedLine = false;
                        for (int x = 0; x < width; x++)
                        {
                            for (int y = 0; y < height; y++)
                            {
                                if (_elementsReferences[x, y] != null)
                                {
                                    _elementsReferences[x, y].GetComponent<TileScript>().TriggerExplosion();
                                    _elementsReferences[x, y] = null;
                                    if (y == height - 1)
                                        deletedLine = true;
                                }
                            }
                            if (deletedLine)
                                break;
                            if (x == width - 1)
                                allDeleted = true;
                        }
                    }
                    break;

                case CascadeRefill.ExplodeLineByLineRightToLeft:
                    _nextRefillExplosionTimer += Time.deltaTime * 1000;
                    if (_nextRefillExplosionTimer >= DelayBetweenExplosionsInMS)
                    {
                        _nextRefillExplosionTimer = 0;
                        bool deletedLine = false;
                        for (int x = width - 1; x >= 0; x--)
                        {
                            for (int y = height - 1; y >= 0; y--)
                            {
                                if (_elementsReferences[x, y] != null)
                                {
                                    _elementsReferences[x, y].GetComponent<TileScript>().TriggerExplosion();
                                    _elementsReferences[x, y] = null;
                                    if (y == 0)
                                        deletedLine = true;
                                }
                            }
                            if (deletedLine)
                                break;
                            if (x == 0)
                                allDeleted = true;
                        }
                    }
                    break;
                #endregion
                #region One By One
                case CascadeRefill.ExplodeOneByOneFromTopLeft:
                    _nextRefillExplosionTimer += Time.deltaTime * 1000;

                    if (_nextRefillExplosionTimer >= DelayBetweenExplosionsInMS)
                    {
                        _nextRefillExplosionTimer = 0;
                        bool deletedLine = false;
                        for (int y = height - 1; y >= 0; y--)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                if (_elementsReferences[x, y] != null)
                                {
                                    _elementsReferences[x, y].GetComponent<TileScript>().TriggerExplosion();
                                    _elementsReferences[x, y] = null;
                                    deletedLine = true;
                                    break;
                                }
                            }
                            if (deletedLine)
                                break;
                            if (y == 0)
                                allDeleted = true;
                        }
                    }
                    break;
                case CascadeRefill.ExplodeOneByOneFromTopRight:
                    _nextRefillExplosionTimer += Time.deltaTime * 1000;

                    if (_nextRefillExplosionTimer >= DelayBetweenExplosionsInMS)
                    {
                        _nextRefillExplosionTimer = 0;
                        bool deletedLine = false;
                        for (int y = height - 1; y >= 0; y--)
                        {
                            for (int x = width - 1; x >= 0; x--)
                            {
                                if (_elementsReferences[x, y] != null)
                                {
                                    _elementsReferences[x, y].GetComponent<TileScript>().TriggerExplosion();
                                    _elementsReferences[x, y] = null;
                                    deletedLine = true;
                                    break;
                                }
                            }
                            if (deletedLine)
                                break;
                            if (y == 0)
                                allDeleted = true;
                        }
                    }
                    break;
                case CascadeRefill.ExplodeOneByOneFromBottomLeft:
                    _nextRefillExplosionTimer += Time.deltaTime * 1000;

                    if (_nextRefillExplosionTimer >= DelayBetweenExplosionsInMS)
                    {
                        _nextRefillExplosionTimer = 0;
                        bool deletedLine = false;
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                if (_elementsReferences[x, y] != null)
                                {
                                    _elementsReferences[x, y].GetComponent<TileScript>().TriggerExplosion();
                                    _elementsReferences[x, y] = null;
                                    deletedLine = true;
                                    break;
                                }
                            }
                            if (deletedLine)
                                break;
                            if (y == height - 1)
                                allDeleted = true;
                        }
                    }
                    break;
                case CascadeRefill.ExplodeOneByOneFromBottomRight:
                    _nextRefillExplosionTimer += Time.deltaTime * 1000;

                    if (_nextRefillExplosionTimer >= DelayBetweenExplosionsInMS)
                    {
                        _nextRefillExplosionTimer = 0;
                        bool deletedLine = false;
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = width - 1; x >= 0; x--)
                            {
                                if (_elementsReferences[x, y] != null)
                                {
                                    _elementsReferences[x, y].GetComponent<TileScript>().TriggerExplosion();
                                    _elementsReferences[x, y] = null;
                                    deletedLine = true;
                                    break;
                                }
                            }
                            if (deletedLine)
                                break;
                            if (y == height - 1)
                                allDeleted = true;
                        }
                    }
                    break;
                #endregion
                #region One By One Zig Zag
                case CascadeRefill.ExplodeOneByOneFromTopLeftZigZag:
                    _nextRefillExplosionTimer += Time.deltaTime * 1000;

                    if (_nextRefillExplosionTimer >= DelayBetweenExplosionsInMS)
                    {
                        _nextRefillExplosionTimer = 0;
                        bool deletedLine = false;
                        for (int y = height - 1; y >= 0; y--)
                        {
                            if (y % 2 == 0)
                            {
                                for (int x = 0; x < width; x++)
                                {
                                    if (_elementsReferences[x, y] != null)
                                    {
                                        _elementsReferences[x, y].GetComponent<TileScript>().TriggerExplosion();
                                        _elementsReferences[x, y] = null;
                                        deletedLine = true;
                                        break;
                                    }
                                }
                                if (deletedLine)
                                    break;
                                if (y == 0)
                                    allDeleted = true;
                            }
                            else
                            {
                                for (int x = width - 1; x >= 0; x--)
                                {
                                    if (_elementsReferences[x, y] != null)
                                    {
                                        _elementsReferences[x, y].GetComponent<TileScript>().TriggerExplosion();
                                        _elementsReferences[x, y] = null;
                                        deletedLine = true;
                                        break;
                                    }
                                }
                            }
                            if (deletedLine)
                                break;
                            if (y == 0)
                                allDeleted = true;
                        }
                    }
                    break;
                case CascadeRefill.ExplodeOneByOneFromTopRightZigZag:
                    _nextRefillExplosionTimer += Time.deltaTime * 1000;

                    if (_nextRefillExplosionTimer >= DelayBetweenExplosionsInMS)
                    {
                        _nextRefillExplosionTimer = 0;
                        bool deletedLine = false;
                        for (int y = height - 1; y >= 0; y--)
                        {
                            if (y % 2 != 0)
                            {
                                for (int x = 0; x < width; x++)
                                {
                                    if (_elementsReferences[x, y] != null)
                                    {
                                        _elementsReferences[x, y].GetComponent<TileScript>().TriggerExplosion();
                                        _elementsReferences[x, y] = null;
                                        deletedLine = true;
                                        break;
                                    }
                                }
                                if (deletedLine)
                                    break;
                                if (y == 0)
                                    allDeleted = true;
                            }
                            else
                            {
                                for (int x = width - 1; x >= 0; x--)
                                {
                                    if (_elementsReferences[x, y] != null)
                                    {
                                        _elementsReferences[x, y].GetComponent<TileScript>().TriggerExplosion();
                                        _elementsReferences[x, y] = null;
                                        deletedLine = true;
                                        break;
                                    }
                                }
                            }
                            if (deletedLine)
                                break;
                            if (y == 0)
                                allDeleted = true;
                        }
                    }
                    break;
                case CascadeRefill.ExplodeOneByOneFromBottomLeftZigZag:
                    _nextRefillExplosionTimer += Time.deltaTime * 1000;

                    if (_nextRefillExplosionTimer >= DelayBetweenExplosionsInMS)
                    {
                        _nextRefillExplosionTimer = 0;
                        bool deletedLine = false;
                        for (int y = 0; y < height; y++)
                        {
                            if (y % 2 == 0)
                            {
                                for (int x = 0; x < width; x++)
                                {
                                    if (_elementsReferences[x, y] != null)
                                    {
                                        _elementsReferences[x, y].GetComponent<TileScript>().TriggerExplosion();
                                        _elementsReferences[x, y] = null;
                                        deletedLine = true;
                                        break;
                                    }
                                }
                                if (deletedLine)
                                    break;
                                if (y == height - 1)
                                    allDeleted = true;
                            }
                            else
                            {
                                for (int x = width - 1; x >= 0; x--)
                                {
                                    if (_elementsReferences[x, y] != null)
                                    {
                                        _elementsReferences[x, y].GetComponent<TileScript>().TriggerExplosion();
                                        _elementsReferences[x, y] = null;
                                        deletedLine = true;
                                        break;
                                    }
                                }
                            }
                            if (deletedLine)
                                break;
                            if (y == height - 1)
                                allDeleted = true;
                        }
                    }
                    break;
                case CascadeRefill.ExplodeOneByOneFromBottomRightZigZag:
                    _nextRefillExplosionTimer += Time.deltaTime * 1000;

                    if (_nextRefillExplosionTimer >= DelayBetweenExplosionsInMS)
                    {
                        _nextRefillExplosionTimer = 0;
                        bool deletedLine = false;
                        for (int y = 0; y < height; y++)
                        {
                            if (y % 2 != 0)
                            {
                                for (int x = 0; x < width; x++)
                                {
                                    if (_elementsReferences[x, y] != null)
                                    {
                                        _elementsReferences[x, y].GetComponent<TileScript>().TriggerExplosion();
                                        _elementsReferences[x, y] = null;
                                        deletedLine = true;
                                        break;
                                    }
                                }
                                if (deletedLine)
                                    break;
                                if (y == 0)
                                    allDeleted = true;
                            }
                            else
                            {
                                for (int x = width - 1; x >= 0; x--)
                                {
                                    if (_elementsReferences[x, y] != null)
                                    {
                                        _elementsReferences[x, y].GetComponent<TileScript>().TriggerExplosion();
                                        _elementsReferences[x, y] = null;
                                        deletedLine = true;
                                        break;
                                    }
                                }
                            }
                            if (deletedLine)
                                break;
                            if (y == height - 1)
                                allDeleted = true;
                        }
                    }
                    break;
                #endregion
                default:
                    break;
            }
            if (allDeleted)
            {
                _cantShuffle = false;
                RepopulateGridWhenNoMoreMoves();
                _refillTimer = 0;
            }
        }
    }
    /// <summary>
    /// Performs the behavior selected on shuffleBehavior, MUST be called from Update() or FixedUpdate();
    /// </summary>
    private void ShuffleScript()
    {
        if (_cantShuffle)
        {
            RefillScript();
            return;
        }

        switch (shuffleBehavior)
        {
            case ShuffleRefill.GoToCenterThenShuffle:
                if (!_shufflePerformed)
                    Shuffle();
                else if (_shufflePerformed)
                {
                    if (_shuffleTimer < shuffleDelayInMS)
                        _shuffleTimer += Time.deltaTime * 1000;
                    else if (!_areTilesIntheMiddle && !_isShuffleRearrangingInPlace)
                    {
                        for (int x = 0; x < width; x++)
                            for (int y = 0; y < height; y++)
                                _elementsReferences[x, y].GetComponent<TileScript>().MoveTo(TransformV2(), cascadeTimeDuration);
                        _isShuffleRearrangingInPlace = true;
                    }
                    else if (_isShuffleRearrangingInPlace && !_areTilesIntheMiddle)
                    {
                        _areTilesIntheMiddle = true;
                        foreach (GameObject go in _elementsMovingReferences)
                            if (go.GetComponent<TileScript>().IsMoving)
                            {
                                _areTilesIntheMiddle = false;
                                break;
                            }
                        if (_areTilesIntheMiddle)
                            _isShuffleRearrangingInPlace = false;
                    }
                    else if (!_isShuffleRearrangingInPlace && _areTilesIntheMiddle)
                    {
                        if (_shuffleTimer < (shuffleDelayInMS + shuffleTimeSpentWaitingInCenterInMS))
                            _shuffleTimer += Time.deltaTime * 1000;
                        else
                        {
                            for (int x = 0; x < width; x++)
                                for (int y = 0; y < height; y++)
                                    _elementsReferences[x, y].GetComponent<TileScript>().MoveTo(_coordinates[x, y] + TransformV2(), cascadeTimeDuration);
                            _isShuffleRearrangingInPlace = true;
                        }

                    }
                    else if (_isShuffleRearrangingInPlace && _areTilesIntheMiddle)
                    {
                        bool finisheMoving = true;
                        foreach (GameObject go in _elementsMovingReferences)
                            if (go.GetComponent<TileScript>().IsMoving)
                            {
                                finisheMoving = false;
                                break;
                            }
                        if (finisheMoving)
                        {
                            _noMoreMoves = false;
                            _shufflePerformed = false;
                            _isShuffleRearrangingInPlace = false;
                            _areTilesIntheMiddle = false;
                            EnableColliders(true);
                            _shuffleTimer = 0;
                        }
                    }
                }
                break;
            case ShuffleRefill.ShuffleAllAtOnce:
                if (!_shufflePerformed)
                    Shuffle();
                else if (!_isShuffleRearrangingInPlace)
                {
                    if (_shuffleTimer < shuffleDelayInMS)
                        _shuffleTimer += Time.deltaTime * 1000;
                    else
                    {
                        for (int x = 0; x < width; x++)
                            for (int y = 0; y < height; y++)
                                _elementsReferences[x, y].GetComponent<TileScript>().MoveTo(_coordinates[x, y] + TransformV2(), cascadeTimeDuration);
                        _isShuffleRearrangingInPlace = true;
                    }
                }
                else
                {
                    _isShuffleRearrangingInPlace = false;
                    foreach (var go in _elementsReferences)
                        if (go.GetComponent<TileScript>().IsMoving)
                        {
                            _isShuffleRearrangingInPlace = true;
                            break;
                        }
                    if (!_isShuffleRearrangingInPlace)
                    {
                        _noMoreMoves = false;
                        _shufflePerformed = false;
                        EnableColliders(true);
                        _shuffleTimer = 0;
                    }
                }
                break;
        }
    }
    /// <summary>
    /// Activates the hint on a random tile where a match can be performed.
    /// </summary>
    private void ActivateHint()
    {
        List<Vector2> listOfMoves = GetAllAvailableMoves();
        if (listOfMoves.Count == 0)
        {
            return;
        }
        Vector2 currentHint = listOfMoves[Random.Range(0, listOfMoves.Count)];

        _currentHintedTile = _elementsReferences[(int)currentHint.x, (int)currentHint.y];
        _currentHintedTile.GetComponent<TileScript>().ActivateHint(true);

        _isHintInPlace = true;

    }
    /// <summary>
    /// deactivates the hint on the hinted tile.
    /// </summary>
    private void DeactivateHint()
    {
        if (_currentHintedTile != null)
        {
            _currentHintedTile.GetComponent<TileScript>().ActivateHint(false);
            _currentHintedTile = null;
        }
        _isHintInPlace = false;
        _hintTimer = 0;
    }
    #endregion

}

