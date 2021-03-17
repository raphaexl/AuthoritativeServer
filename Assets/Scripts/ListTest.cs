using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ListTest : MonoBehaviour
{

    public class Person
    {
        public string Name;
        public int Age;

        public Person(string Name, int Age)
        {
            this.Name = Name;
            this.Age = Age;
        }

        public override string ToString()
        {
            return $"Name : {this.Name} Age : {this.Age}";
        }
    }
    List<Person> students;
    List<Person> persons;
    // Start is called before the first frame update

    List<Person> TestModification(List<Person> people)
    {
        Person afi = people[0];
        afi.Name = "Afi";
        return (people);
    }

    void Start()
    {
        persons = new List<Person>();
        persons.Add(new Person("Ama", 25));
        persons.Add(new Person("Abalo", 21));
        persons.Add(new Person("Remi", 26));

        students = persons;
        //     students.Clear();
        Debug.Log("Before Modification");
        persons.ForEach(person =>
        {
            Debug.Log(person);
        });
        students = TestModification(students);
        
        Debug.Log("After Modification");
        students.ForEach(person =>
        {
            Debug.Log(person);
        });
    }


}
