using System.Reflection;
using System.Reflection.Metadata;
using Considition2023_Cs;

public class GeneticSearch{

        static Random Rnd = new Random(500);
        public static async void Run(MapData data, GeneralData generalData)
        {
            var size = data.locations.Count;
            var male = RandomArray(size);
            var female = RandomArray(size);
            var children = new (int, int)[10][];
            MakeChildren(children, male, female);
        }

        private static (int, int)[] RandomArray(int size){
            
            (int, int)[] a = new (int, int)[size];
            for (int i = 0; i < size; i++)
            {
                a[i] = (Rnd.Next(5), Rnd.Next(5));
            }
            return a;
        }

        private static void MakeChildren((int, int)[][] children, (int, int)[] male, (int, int)[] female){
            for (int i = 0; i < children.Length; i++)
            {
                var split = Rnd.Next(male.Length);
                Array.Copy(male,0, children[i], 0, split);
                Array.Copy(female, split, children[i], split, female.Length - split);
                var mutation = Rnd.Next(male.Length);
            }
        }
}