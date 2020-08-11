using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Mob 
{

    const int NUMBERCOMMAND = 88 ,NUMBERGEN = 80;

    public int ID { private set; get; }

    Controller boss;
    int steps = 0;
    public bool dead = false;
    public bool colony = false;
    public Dna myDna,DnaCopy;
    public int x, y;
    int numberCommand = 0;
    public int energy;
    int maxCommand;
    int direction = 0;

    public Mob(Dna dna,int x,int y, int energy,int maxCommand, Controller boss,int ID,bool colony)
    {
        this.myDna = new Dna();
        this.myDna.DnaProgramm = (byte[])dna.DnaProgramm.Clone();
        this.DnaCopy = new Dna(); 
        this.DnaCopy.DnaProgramm = (byte[])dna.DnaProgramm.Clone();
        this.x = x;
        this.y = y;
        this.energy = energy;
        this.maxCommand = maxCommand;
        this.boss = boss;
        this.ID = ID;
        
    }

    public Mob (bool dead,int x, int y)
    {
        DeadMob();
        this.x = x;
        this.y = y;
    }

    public void StartProgramm()
    {
        numberCommand = myDna.DnaProgramm[0];
        CheckLive();
        if (!dead)
        {
            steps++;
            for (int i = 0; i < maxCommand;i++)
            {
                NextCommand();
    
                if (myDna.DnaProgramm[numberCommand] < 8)
                {
                    i+=2;
                    Move();
                }
                else if (myDna.DnaProgramm[numberCommand] < 16)
                {
                    Rotate();
                }
                else if (myDna.DnaProgramm[numberCommand] < 24)
                {
                    i += 5;
                    Eat();
                }
                else if (myDna.DnaProgramm[numberCommand] < 32)
                {
                    CheckEnergy();
                }
                else if (myDna.DnaProgramm[numberCommand] < 40)
                {
                    i += 4;
                    Hit();
                }
                else if (myDna.DnaProgramm[numberCommand] < 48)
                {
                    i += 10;
                    Photosynthesis();
                }
                else if (myDna.DnaProgramm[numberCommand] < 56)
                {
                    i += 7;
                    Breeding(false);
                }
                else if (myDna.DnaProgramm[numberCommand] < 64)
                {
                    Transition();
                }
                else if (myDna.DnaProgramm[numberCommand] < 72)
                {
                    i += 7;
                    Breeding(true);
                }
                else if (myDna.DnaProgramm[numberCommand] < 80)
                {
                    TransferEnergeOut();
                }
                else if (myDna.DnaProgramm[numberCommand] < 88)
                {
                    i += 12;
                    Infection();
                }
                else if (myDna.DnaProgramm[numberCommand] < 96)
                {
                    i += 12;
                    Fire();
                }
            }
            energy--;
            CheckLive();
            if (steps == 1000)
            {
                DeadMob();
            }
            if (energy > 255)
            {
                energy = 255;
            }
        }
    }

    void Move()
    {
        
        int nextX=x, nextY=y;
        switch ((myDna.DnaProgramm[numberCommand % NUMBERGEN] + direction) % 8)
        {
            case 0:
                nextY++;
                break;
            case 1:
                nextY++;
                nextX++;
                break;
            case 2:
                nextX++;
                break;
            case 3:
                nextY--;
                nextX++;
                break;
            case 4:
                nextY--;
                break;
            case 5:
                nextY--;
                nextX--;
                break;
            case 6:
                nextX--;
                break;
            case 7:
                nextY++;
                nextX--;
                break;
            default:
                nextY++;
                break;
        }
        if (!colony)
        {
            int[] result = boss.MoveMob(nextX, nextY, this);

            if (result[2] == 1)
            {
                x = result[0];
                y = result[1];
            }
            numberCommand += myDna.DnaProgramm[(numberCommand + result[2]) % NUMBERGEN];
        }
        numberCommand += myDna.DnaProgramm[(numberCommand + 5) % NUMBERGEN];


    }

    void Rotate()
    {
        //Debug.Log(myDna.DnaProgramm.Length + " " + numberCommand % NUMBERCOMMAND + " " + (myDna.DnaProgramm[numberCommand % NUMBERCOMMAND] - 8 * direction) % NUMBERCOMMAND);
        direction = Mathf.Abs((myDna.DnaProgramm[numberCommand % NUMBERGEN] % 8 + direction)) % 8;

        int nextX = x, nextY = y;
        switch ((myDna.DnaProgramm[numberCommand % NUMBERGEN] + direction) % 8)
        {
            case 0:
                nextY++;
                break;
            case 1:
                nextY++;
                nextX++;
                break;
            case 2:
                nextX++;
                break;
            case 3:
                nextY--;
                nextX++;
                break;
            case 4:
                nextY--;
                break;
            case 5:
                nextY--;
                nextX--;
                break;
            case 6:
                nextX--;
                break;
            case 7:
                nextY++;
                nextX--;
                break;
            default:
                nextY++;
                break;
        }

        int result = boss.See(nextX, nextY, this);
        numberCommand += myDna.DnaProgramm[(numberCommand + result) % NUMBERGEN];


    }

    void Eat()
    {
        int nextX = x, nextY = y;
        switch (direction)
        {
            case 0:
                nextY++;
                break;
            case 1:
                nextY++;
                nextX++;
                break;
            case 2:
                nextX++;
                break;
            case 3:
                nextY--;
                nextX++;
                break;
            case 4:
                nextY--;
                break;
            case 5:
                nextY--;
                nextX--;
                break;
            case 6:
                nextX--;
                break;
            case 7:
                nextY++;
                nextX--;
                break;
            default:
                nextY++;
                break;
        }
        int target = boss.Eat(nextX, nextY, this);
        
        numberCommand += myDna.DnaProgramm[(numberCommand + target) % NUMBERGEN];
    }

    void CheckEnergy()
    {
        boss.CheсkEnergy();

        if (energy > myDna.DnaProgramm[(numberCommand + 1) % NUMBERGEN] * numberCommand)
        {
            numberCommand += myDna.DnaProgramm[(myDna.DnaProgramm[numberCommand % NUMBERGEN] * myDna.DnaProgramm[(numberCommand  + 1) % NUMBERGEN]) % NUMBERGEN];
        }
        else
        {
            numberCommand += myDna.DnaProgramm[(myDna.DnaProgramm[numberCommand % NUMBERGEN] * myDna.DnaProgramm[(numberCommand  + 2) % NUMBERGEN]) % NUMBERGEN];
        }
       
    }

    void Hit()
    {
        int nextX = x, nextY = y;
        switch ( direction)
        {
            case 0:
                nextY++;
                break;
            case 1:
                nextY++;
                nextX++;
                break;
            case 2:
                nextX++;
                break;
            case 3:
                nextY--;
                nextX++;
                break;
            case 4:
                nextY--;
                break;
            case 5:
                nextY--;
                nextX--;
                break;
            case 6:
                nextX--;
                break;
            case 7:
                nextY++;
                nextX--;
                break;
            default:
                nextY++;
                break;
        }
        int target = boss.Hit(nextX, nextY, this);

        numberCommand += myDna.DnaProgramm[(numberCommand + target) % NUMBERGEN];
    }

    void Photosynthesis()
    {
        boss.Photosintes(this);
        numberCommand += myDna.DnaProgramm[(numberCommand + 1) % NUMBERGEN];
    }

    void Breeding(bool colony)
    {
        
        if (energy >= myDna.DnaProgramm[numberCommand])
        {
            int nextX = x, nextY = y;
            switch ((myDna.DnaProgramm[numberCommand % NUMBERGEN] % 8 + direction) % 8)
            {
                case 0:
                    nextY++;
                    break;
                case 1:
                    nextY++;
                    nextX++;
                    break;
                case 2:
                    nextX++;
                    break;
                case 3:
                    nextY--;
                    nextX++;
                    break;
                case 4:
                    nextY--;
                    break;
                case 5:
                    nextY--;
                    nextX--;
                    break;
                case 6:
                    nextX--;
                    break;
                case 7:
                    nextY++;
                    nextX--;
                    break;
                default:
                    nextY++;
                    break;
            }
            int target = boss.Deliver(nextX, nextY, this, colony, myDna.DnaProgramm[(numberCommand + 1) % NUMBERGEN]);
            if (colony)
            {
                this.colony = true;
            }
            numberCommand += myDna.DnaProgramm[(numberCommand + target) % NUMBERGEN];
        }
        else
        {
            numberCommand += myDna.DnaProgramm[(numberCommand + 4) % NUMBERGEN];
        }
    }

    void Transition()
    {
        boss.Trans();
        numberCommand += myDna.DnaProgramm[(numberCommand + NUMBERCOMMAND - numberCommand) % NUMBERGEN] * myDna.DnaProgramm[numberCommand % NUMBERGEN];
    }

    void NextCommand()
    {
        numberCommand = numberCommand % NUMBERGEN;
    }

    public void CheckLive()
    {
        if (energy <= 0)
        {
            DeadMob();
        }
    }

    public int GetDamage(int proc)
    {
        int damage = (int)Mathf.Ceil(energy / 100f * proc);
        energy -= damage + 5;
        return damage;
    }

    public void AddEnergy(int food)
    {
        energy += food;
    }

    public int GetEnergy()
    {
        return energy;
    }

    public int TransferEnergeIn(int proc)
    {
        int damage = (int)Mathf.Ceil(energy / 100f * proc);
        energy -= damage;
        return damage;
    }

    public void TransferEnergeOut()
    {
        int nextX = x, nextY = y;
        switch (direction)
        {
            case 0:
                nextY++;
                break;
            case 1:
                nextY++;
                nextX++;
                break;
            case 2:
                nextX++;
                break;
            case 3:
                nextY--;
                nextX++;
                break;
            case 4:
                nextY--;
                break;
            case 5:
                nextY--;
                nextX--;
                break;
            case 6:
                nextX--;
                break;
            case 7:
                nextY++;
                nextX--;
                break;
            default:
                nextY++;
                break;
        }
        int food = (int)energy / 5;
        energy -= food;
        int target = boss.TrnsferOut(nextX, nextY, this, food);

        numberCommand += myDna.DnaProgramm[(numberCommand + target) % NUMBERGEN];
    }

    public void Infection()
    {
        int nextX = x, nextY = y;
        switch (direction)
        {
            case 0:
                nextY++;
                break;
            case 1:
                nextY++;
                nextX++;
                break;
            case 2:
                nextX++;
                break;
            case 3:
                nextY--;
                nextX++;
                break;
            case 4:
                nextY--;
                break;
            case 5:
                nextY--;
                nextX--;
                break;
            case 6:
                nextX--;
                break;
            case 7:
                nextY++;
                nextX--;
                break;
            default:
                nextY++;
                break;
        }       
        int target = boss.Infection(nextX, nextY, this);

        numberCommand += myDna.DnaProgramm[(numberCommand + target) % NUMBERGEN];
    }

    void Fire()
    {

        int nextX = x, nextY = y;        

        int result = boss.Fire(x, y, (myDna.DnaProgramm[numberCommand % NUMBERGEN] + direction) % 8, this);         
        numberCommand += myDna.DnaProgramm[(numberCommand + result) % NUMBERGEN];
        numberCommand += myDna.DnaProgramm[(numberCommand + 5) % NUMBERGEN];


    }

    public void DeadMob()
    {
        dead = true;

    }

}
