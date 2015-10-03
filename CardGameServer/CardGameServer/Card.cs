﻿using System;
using System.Collections.Generic;

namespace CardGameServer
{
    /// <summary>
    /// Used for all cards in game.
    /// !! Use a base type and create instances of it !!
    /// </summary>
    public class Card
    {
        public Guid UID;
        public String ID;
        public CardType Type;
        public int Cost;
        public string CreatureID;
        public string SpellID;
        public List<EffectData> EffectData = new List<EffectData>(); 
        public List<IEffect<Card>> Effects;
        public Player Owner;
        public bool CancelPlay = false;
        public event Action<Card> Played;

        protected virtual void OnPlayed(Card obj)
        {
            var handler = Played;
            if (handler != null) handler(obj);
        }

        public bool Play(Board b)
        {
            OnPlayed(this);

            switch (Type)
            {
                case CardType.Creature:
                    Game.Board.Summon(Owner, Game.Creatures[CreatureID]);
                    break;
                case CardType.Spell:
                    Game.Board.Cast(Owner, Game.Spells[SpellID]);
                    break;
            }

            return !CancelPlay;
        }

        public Card CreateInstance()
        {
            var card = new Card
            {
                ID = ID,
                Type = Type,
                CreatureID = CreatureID,
                SpellID = SpellID,
                Cost = Cost,
                UID = Guid.NewGuid()
            };

            foreach (var effect in EffectData)
            {
                // Need to create a new instance of effect for every creature!
                card.Effects.Add(Game.CardEffects.CreateInstance(effect.Name, effect.Attributes));
            }

            return card;
        }
    }

    public enum CardType
    {
        Creature,
        Spell
    }
}