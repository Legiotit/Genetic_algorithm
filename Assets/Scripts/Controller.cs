using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class Controller : MonoBehaviour 
{

    
    const int MINIMUMX = 0, MINIMUMY = 0, RANGEFIRE = 3;

    public int maxX { get; set; }
    public int maxY { get; set; }
    int numberID = 0;
    public Mob[,] Map;
    public GameObject[,] Pics;                  
    List<Mob> MobsList = new List<Mob>();
    List<Mob> FoodList = new List<Mob>();
    [SerializeField] private int maxCommand = 35;
    [SerializeField] private bool draw;
    [SerializeField] private GameObject pic;
    public bool activeProgramm;
    bool startProgramm = false;

    [SerializeField] InputField inputX, inputY;

    int step = 0;
    public bool energyView = false;

    public Text text;
    public float time;

    // Use this for initialization
    void Start () 
    {
        maxX = 100;
        maxY = 100;
        //CreateMap();
    }
	
	public void CreateMap()
    {
        ChangeSize();
        Map = new Mob[maxX, maxY];
        Pics = new GameObject[maxX, maxY];
        this.maxX = maxX;
        this.maxY = maxY;

        CreatePics(pic);
        Spawn();
        startProgramm = true;
    }

	void Update () 
    {
        if (activeProgramm && startProgramm)
        {
            text.text = step.ToString();
            step++;
            UpdataList();
            //Debug.Log(MobsList.Count + " " + FoodList.Count+" " + step );
            for (int i=0;i<MobsList.Count;i++)
            {
                MobsList[i].StartProgramm();
            }
            if (MobsList.Count < 1000)
            {
                Spawn();
            }
            Draw();
        }
	}

    public int[] MoveMob(int x, int y, Mob myClass)
    {

        x = CheakPosX(x);
        y = CheakPosY(y);

        if (Map[x,y] == null )
        {
            Map[myClass.x, myClass.y] = null;
            Map[x, y] = myClass;
            return new int[] { x, y, (int)TypeObject.Vacuum };
        }
        else if (Map[x, y].dead)
        {
            return new int[] { 0, 0, (int)TypeObject.Food };
        }
        else if (Map[x, y].myDna.RelationFriend(myClass.myDna))
        {
            //Debug.Log(friend);
            return new int[] { 0, 0, (int)TypeObject.Friend };
        }
        else
        {
            //Debug.Log(enemy);
            return new int[] { 0, 0, (int)TypeObject.Enemy };
        }
    }

    public int See(int x, int y, Mob myClass)
    {
        x = CheakPosX(x);
        y = CheakPosY(y);

        if (Map[x, y] == null)
        {
            Map[myClass.x, myClass.y] = null;
            Map[x, y] = myClass;
            return (int)TypeObject.Vacuum;
        }
        else if (Map[x, y].dead)
        {
            return (int)TypeObject.Food;
        }
        else if (Map[x, y].myDna.RelationFriend(myClass.myDna))
        {
            //Debug.Log(friend);
            return (int)TypeObject.Friend;
        }
        else
        {
            //Debug.Log(enemy);
            return (int)TypeObject.Enemy;
        }

    }

    public int Eat(int x, int y, Mob myClass)
    {
        x = CheakPosX(x);
        y = CheakPosY(y);

        if (Map[x, y] == null)
        {
            return (int)TypeObject.Vacuum;
        }
        else if (Map[x, y].dead)
        {
            Map[x, y] = null;
            myClass.AddEnergy(23);
            return (int)TypeObject.Food;
        }
        else if (Map[x, y].myDna.RelationFriend(myClass.myDna))
        {
           if (myClass.colony)
            {
                //myClass.TransferEnergeIn(15);
            }
            else
            {
                myClass.AddEnergy(Map[x, y].GetDamage(20)-5);
            }           
            return (int)TypeObject.Friend;
        }
        else
        {
            //Debug.Log(enemy);
            myClass.AddEnergy(Map[x, y].GetDamage(20)-5);
            return (int)TypeObject.Enemy;
        }
    }

    public int TrnsferOut(int x, int y, Mob myClass ,int food)
    {
        x = CheakPosX(x);
        y = CheakPosY(y);

        if (Map[x, y] == null)
        {
            return (int)TypeObject.Vacuum;
        }
        else if (Map[x, y].dead)
        {
            return food;
        }
        else if (Map[x, y].myDna.RelationFriend(myClass.myDna))
        {
            if (myClass.colony)
            {
                Map[x, y].AddEnergy(food);
            }
            else
            {
                if (food - 3 > 0)
                {
                    Map[x, y].AddEnergy(food - 3);
                }               
            }
            return (int)TypeObject.Friend;
        }
        else
        {
            //Debug.Log(enemy);
            if (food - 3 > 0)
            {
                Map[x, y].AddEnergy(food - 3);
            }
            return (int)TypeObject.Enemy;
        }
    }

    int CheakPosX(int x)
    {
        if (x >= maxX)
        {
            return x % maxX;
        }
        else if (x < MINIMUMX)
        {
            return x + maxX ;
        }
        else
        {
            return x;
        }
    }

    int CheakPosY(int y)
    {
        if (y >= maxY)
        {
            return y % maxY;
        }
        else if (y < MINIMUMY)
        {
            return y + maxY;
        }
        else
        {
            return y;
        }
    }

    public int Hit(int x, int y, Mob myClass)
    {
        x = CheakPosX(x);
        y = CheakPosY(y);

        if (Map[x, y] == null)
        {
            return (int)TypeObject.Vacuum;
        }
        else if (Map[x, y].dead)
        {
            Map[x, y] = null;
            return (int)TypeObject.Food;
        }
        else if (Map[x, y].myDna.RelationFriend(myClass.myDna))
        {
            if (myClass.colony) { }//Map[x, y].GetDamage(45);
            else Map[x, y].GetDamage(45);
            return (int)TypeObject.Friend;
        }
        else
        {
            // Debug.Log(enemy);
            Map[x, y].GetDamage(60);
            return (int)TypeObject.Enemy;
        }
    }

    public int Deliver(int x, int y, Mob myClass,bool colony,byte number)
    {
        x = CheakPosX(x);
        y = CheakPosY(y);

        if (Map[x, y] == null)
        {
            int energy = myClass.GetDamage(45);
            Dna dna = new Dna();
            dna.DnaProgramm = (byte[])myClass.DnaCopy.DnaProgramm.Clone();
            dna.DnaProgramm[0] = number;
            if (Random.Range(0, 100) < 12) dna.Mutate();
            CreateMob(dna, x, y, energy, colony);
            return 2;
        }
        else 
        {
            return 3;
        }
        
    }

    public void CheсkEnergy()
    {

    }

    public void Photosintes(Mob myClass)
    {
        int x = myClass.x;
        int y = myClass.y;
        float dist = Vector2.Distance(new Vector2((maxX - MINIMUMX) / 2, (maxY - MINIMUMY) / 2), new Vector2(x, y));
        float disttozero = Vector2.Distance(new Vector2((maxX - MINIMUMX) / 2, (maxY - MINIMUMY) / 2), new Vector2(MINIMUMX, MINIMUMY));
        myClass.AddEnergy(Mathf.RoundToInt(Mathf.Ceil(Mathf.Sin((disttozero - dist) / disttozero * Mathf.PI / 2) * 5f)));
    }

    public void Trans()
    {

    }

    public int Infection(int x, int y, Mob myClass)
    {
        x = CheakPosX(x);
        y = CheakPosY(y);

        if (Map[x, y] == null)
        {
            return (int)TypeObject.Vacuum;
        }
        else if (Map[x, y].dead)
        {
            return (int)TypeObject.Food;
        }
        else if (Map[x, y].myDna.RelationFriend(myClass.myDna))
        {
            if (myClass.colony) Map[x, y].DnaCopy.DnaProgramm = (byte[])myClass.DnaCopy.DnaProgramm.Clone();
            else Map[x, y].DnaCopy.DnaProgramm = (byte[])myClass.DnaCopy.DnaProgramm.Clone();
            return (int)TypeObject.Friend;
        }
        else
        {
            // Debug.Log(enemy);
            Map[x, y].DnaCopy.DnaProgramm = (byte[])myClass.DnaCopy.DnaProgramm.Clone();
            return (int)TypeObject.Enemy;
        }
    }

    public int Fire(int x,int y, int direction ,Mob myClass)
    {
        for (int i = 0; i < RANGEFIRE; i++)
        {
            switch ((direction))
            {
                case 0:
                    y++;
                    break;
                case 1:
                    y++;
                    x++;
                    break;
                case 2:
                    x++;
                    break;
                case 3:
                    y--;
                    x++;
                    break;
                case 4:
                    y--;
                    break;
                case 5:
                    y--;
                    x--;
                    break;
                case 6:
                    x--;
                    break;
                case 7:
                    y++;
                    x--;
                    break;
                default:
                    y++;
                    break;
            }

            x = CheakPosX(x);
            y = CheakPosY(y);
            if (Map[x, y] != null)
            {
                if (Map[x, y].dead)
                {
                    Map[x, y] = null;                   
                    return (int)TypeObject.Food;
                }
                else if (Map[x, y].myDna.RelationFriend(myClass.myDna))
                {
                    if (myClass.colony)
                    {
                        Map[x, y].GetDamage(20);
                    }
                    else
                    {
                        Map[x, y].GetDamage(25);
                    }
                    return (int)TypeObject.Friend;
                }
                else
                {
                    //Debug.Log(enemy);
                    Map[x, y].GetDamage(35);
                    return (int)TypeObject.Enemy;
                }
            }
        }
        return (int)TypeObject.Vacuum;      
    }

    public void UpdataList()
    {
        for (int i = MINIMUMX; i < maxX; i++)
        {
            for (int j = MINIMUMY; j < maxY; j++)
            {
                if (Map[i, j] != null && Map[i, j].GetEnergy() <= 0)
                {
                    Map[i, j].DeadMob();
                }
            }
        }
        FoodList.Clear();
        MobsList.Clear();

        for (int i = MINIMUMX; i < maxX; i++)
        {
            for (int j = MINIMUMY; j < maxY; j++)
            {
                if (Map[i, j] != null)
                {
                    if (Map[i, j].dead)
                    {
                        FoodList.Add(Map[i, j]);
                    }
                    else
                    {
                        MobsList.Add(Map[i, j]);
                    }
                }
                
            }
        }       
    }

    private bool FakeFood(Mob mob)
    {
        if (Map[mob.x, mob.y] == null)
        {
            return true;
        }
        return mob.ID != Map[mob.x, mob.y].ID;
    }
    private bool FakeMob(Mob mob)
    {
        if (mob.dead)
        {
            return true;
        }
        if (Map[mob.x, mob.y] == null)
        {        
            return true;
        }      
        return mob.ID != Map[mob.x,mob.y].ID;
    }

    public bool CreateMob(Dna dna,int x,int y,int energy,bool colony)
    {


        if (Map[x, y] == null)
        {
            
            Mob child = new Mob(dna, x, y, energy, maxCommand, this, numberID, colony);
            Map[x, y] = child;           
            numberID++;
            return true;
        }
        else
        {
            return false;
        }
        
    }

    public void Draw()
    {
        if (draw)
        {
            for (int i = MINIMUMX; i < maxX; i++)
            {
                for (int j = MINIMUMY; j < maxY; j++)
                {
                    if (Map[i,j] != null)
                    {
                        Pics[i, j].GetComponent<Stats>().energy = Map[i, j].GetEnergy();

                        if (Map[i, j].dead)
                        {
                            Pics[i, j].GetComponent<SpriteRenderer>().color = Color.black;
                            Pics[i, j].SetActive(true);
                        }
                        else 
                        {
                            if (energyView)
                            {
                                Pics[i, j].GetComponent<SpriteRenderer>().color = ColorEnergy(Map[i, j].GetEnergy());
                                Pics[i, j].SetActive(true);
                            }
                            else
                            {
                                int R = 0, G = 0, B = 0;
                                for (int z = 0; z < 5; z++)
                                {
                                    R += Map[i, j].myDna.DnaProgramm[z];
                                }
                                for (int z = 22; z < 26; z++)
                                {
                                    G += Map[i, j].myDna.DnaProgramm[z];
                                }
                                for (int z = 43; z < 47; z++)
                                {
                                    B += Map[i, j].myDna.DnaProgramm[z];
                                }

                                Pics[i, j].GetComponent<SpriteRenderer>().color = ColorConvert(R % 255, G % 255, B % 255);
                                Pics[i, j].SetActive(true);
                            }
                        }
                        
                    }
                    else
                    {
                        //Pics[i, j].GetComponent<SpriteRenderer>().color = Color.clear;
                        Pics[i, j].SetActive(false);
                    }
                }
            }
        }
    }

    Color ColorConvert(int R,int G, int B)
    {
        if (R >= G -10 && R <= G + 10)
        {
            B = B % 30;
        }
        else if (R >= B - 10 && R <= B + 10)
        {
            G = G % 30;
        }
        else if (G >= B - 10 && G <= B + 10)
        {
            R = R % 30;
        }
        else if (R > G && R > B)
        {
            B = B % 30;
            G = G % 30;
        }
        else if (G > B)
        {
            R = R % 30;
            B = B % 30;
        }
        else 
        {
            R = R % 30;
            G = G % 30;
        }
        

        return new Color(R / 255f, G / 255f, B / 255f);
    } 

    Color ColorEnergy(int energy)
    {
        return new Color(energy / 255f, energy / 255f, energy / 255f);
    }

    public void CreatePics(GameObject pic)
    {
        for(int i = MINIMUMX; i < maxX; i++)
        {
            for (int j = MINIMUMY; j < maxY; j++)
            {
                GameObject nextpic = Instantiate(pic, transform);
                nextpic.transform.localPosition = new Vector3(i, j, 0);
                Pics[i, j] = nextpic;
                nextpic.SetActive(true);
            }
        }

    }

    void Spawn()
    {
        for (int i = 0; i < 30; i++)
        {
            Dna nextDna = new Dna();
            nextDna.RandomDna();
            int x = Random.Range(MINIMUMX, maxX);
            int y = Random.Range(MINIMUMY, maxY);
            if (Map[x, y] == null)
            {
                CreateMob(nextDna, x, y, 20,false);
            }
        }
    }

    void SpawnFood()
    {
        for (int i = 0; i < 30; i++)
        {
            Dna nextDna = new Dna();
            nextDna.RandomDna();
            int x = Random.Range(MINIMUMX, maxX);
            int y = Random.Range(MINIMUMY, maxY);
            if (Map[x, y] == null)
            {
                CreateMob(nextDna, x, y, 0,false);
            }
        }
    }

    public void AddFood(Mob food)
    {
        FoodList.Add(food);
    }

    void ClearFood()
    {
        if (FoodList.Count > 1000)
        {
            int clearFoodCount = FoodList.Count / 20;
            for (int i =0;i < clearFoodCount; i++)
            {
                Map[FoodList[i].x, FoodList[i].y] = null;
                FoodList.RemoveAt(i);
            }
        }
    }

    public void ViewSelect()
    {
        draw = !draw;
    }

    public void ModeSelect()
    {
        energyView = !energyView;
    }

    public void Pause()
    {
        activeProgramm = !activeProgramm;
    }

    public void ChangeSize()
    {
        //Debug.Log(inputX.textComponen);
        if (inputX != null) maxX = Mathf.Clamp(int.Parse(inputX.text),1,int.MaxValue);
        if (inputY != null) maxY = Mathf.Clamp(int.Parse(inputY.text), 1, int.MaxValue);
    }
}
