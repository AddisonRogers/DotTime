package main

import (
	"fmt"
	"github.com/mitchellh/go-ps"
	"log"
	"time"
)

func main() {

	processList := make(map[int]string)

	for {
		// Get the current system processes
		processes, err := ps.Processes()
		checkErr(err)

		// Prepare a map of the current processes
		processMap := make(map[int]string)
		for _, process := range processes {
			processMap[process.Pid()] = process.Executable()
		}

		// Compare the current process map with the previous one
		if !isSubset(processList, processMap) {
			newProcesses := difference(processMap, processList)
			stoppedProcesses := difference(processList, processMap)
			fmt.Printf("New processes: %v\n", newProcesses)
			fmt.Printf("Stopped processes: %v\n", stoppedProcesses)
		}

		/*
			for _, process := range processes {

				//fmt.Printf("PID: %d, Name: %s\n", process.Pid(), process.Executable())

				// Add the process information to the map
				if _, exists := processMap[process.Pid()]; !exists {
					processMap[process.Pid()] = process.Executable()
				}
			}
		*/

		processList = processMap

		time.Sleep(time.Second)
	}
}

func checkErr(err error) {
	if err != nil {
		log.Fatal(err)
	}
}

func isSubset(first map[int]string, second map[int]string) bool {
	for k, v := range first {
		if secondValue, ok := second[k]; !ok || secondValue != v {
			return false
		}
	}
	return true
}

func difference(first map[int]string, second map[int]string) map[int]string {
	diff := make(map[int]string)
	for k, v := range first {
		if _, ok := second[k]; !ok {
			diff[k] = v
		}
	}
	return diff
}
