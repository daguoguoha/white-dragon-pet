using System;

namespace CodexGirl {
    // Interaction rules adapted from GenhuiC-sudo/11 (MIT); see THIRD_PARTY_NOTICES.md.
    public sealed class PetBehavior {
        public int Mood { get; private set; }
        public bool Sleeping { get; private set; }
        public string Expression { get; private set; }
        public DateTime LastInteraction { get; private set; }
        DateTime expressionUntil, lastDecay, lastStroke = DateTime.MinValue;
        string lastFood;
        int foodStreak;
        public PetBehavior(DateTime now) { Mood = 60; LastInteraction = lastDecay = now; Expression = "idle"; }
        public void Wake(DateTime now) { Sleeping = false; LastInteraction = now; if (Expression == "sleep") Expression = "idle"; }
        public void React(string expression, DateTime now, double seconds) { Wake(now); Expression = expression; expressionUntil = now.AddSeconds(seconds); }
        public void Tick(DateTime now, bool paused) {
            if (now < lastDecay) lastDecay = now;
            int decay = (int)((now - lastDecay).TotalSeconds / 30);
            if (decay > 0) { Mood = Math.Max(0, Mood - decay); lastDecay = lastDecay.AddSeconds(decay * 30); }
            if (paused) { Wake(now); return; }
            if ((now - LastInteraction).TotalSeconds >= 90) { Sleeping = true; Expression = "sleep"; }
            else if (now >= expressionUntil) Expression = "idle";
        }
        void Change(int amount) { Mood = Math.Max(0, Math.Min(100, Mood + amount)); }
        public bool Stroke(DateTime now) {
            Wake(now);
            if ((now - lastStroke).TotalSeconds < 6) return false;
            lastStroke = now; Change(5); React("stroke", now, 3.2); return true;
        }
        public void Poke(DateTime now) { Change(3); React("surprise", now, 2.5); }
        public void Praise(DateTime now) { Change(8); React("happy", now, 3.5); }
        public void Tease(DateTime now) { Change(-2); React("angry", now, 2.8); }
        public static string FoodAnimation(string food) {
            switch (food) {
                case "小鱼干": case "fish": return "eat-fish";
                case "蛋糕": case "cake": return "eat-cake";
                case "棒棒糖": case "lollipop": return "eat-lollipop";
                case "团子": case "dango": return "eat-dango";
                case "钻石": case "diamond": return "eat-diamond";
                default: return "eat";
            }
        }
        public bool Feed(string food, DateTime now) {
            if (food == lastFood) foodStreak++; else { lastFood = food; foodStreak = 1; }
            bool bored = foodStreak >= 3;
            Change(bored ? -1 : 10); React(bored ? "bored" : FoodAnimation(food), now, 3.5); return !bored;
        }
    }
}
