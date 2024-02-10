package main

import (
	"fmt"
	"github.com/mitchellh/go-ps"
	"log"
)

func main() {
	processes, err := ps.Processes()
	checkErr(err)

	for _, process := range processes {
		fmt.Printf("PID: %d, Name: %s\n", process.Pid(), process.Executable())
	}

}

func checkErr(err error) {
	if err != nil {
		log.Fatal(err)
	}
}

// This is going to log all the running processes on the system. It has to be cross platform.
