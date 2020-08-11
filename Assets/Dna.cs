using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Dna  
{
    const int MISS = 1, NUMBERCOMMAND = 96;
    public byte[] DnaProgramm = new byte[80];
    
    public void Mutate()
    {
        DnaProgramm[Random.Range(0, DnaProgramm.Length)] = (byte)Random.Range(0, NUMBERCOMMAND);
    }
    public void RandomDna()
    {
        for (int i=0;i < DnaProgramm.Length; i++)
        {
            DnaProgramm[i] = (byte)Random.Range(0, NUMBERCOMMAND);
        }
    }
    public bool RelationFriend(Dna mobDna)
    {
        byte miss = 0;
        for(int i = 1; i < DnaProgramm.Length;i++)
        {
            if (DnaProgramm[i] != mobDna.DnaProgramm[i])
            {
                miss++;
            }
        }
        if (miss > MISS) return false;
        else return true;
    }
}
