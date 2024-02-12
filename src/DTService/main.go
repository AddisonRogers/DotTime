package main

import (
	"bytes"
	"encoding/base64"
	"encoding/json"
	"errors"
	"fmt"
	"github.com/mitchellh/go-ps"
	"io"
	"log"
	"net/http"
	"os"
	"os/exec"
	"strconv"
	"time"
)

const Version = 1
const Url = "localhost:8080"
const apiKey = ""

type cachedProcess struct {
	Name         string
	PID          int
	timeStarted  *int64
	timeFinished *int64
}

func main() {
	Client := http.DefaultClient
	processList := make(map[int]string)
	flag := 0
	cachedProcesses := new([]cachedProcess)

	for {
		processMap := getProcesses()

		newProcesses := difference(processMap, processList)
		stoppedProcesses := difference(processList, processMap)

		log.Printf("New processes: %v\n", newProcesses)
		log.Printf("Stopped processes: %v\n", stoppedProcesses)

		addNewProcesses(cachedProcesses, newProcesses)
		addFinishedProcesses(cachedProcesses, stoppedProcesses)

		processList = processMap

		if flag%60 == 0 { // Updates the server every minute
			go func() {
				err := sendData(*cachedProcesses, Client)
				checkErr(err)
			}()
		} else if flag == 3600 {
			go func() {
				err := update(Client)
				checkErr(err)
			}()
			flag = 0
		} else {
			flag++
		}

		time.Sleep(time.Second)
	}
}

func checkErr(err error) {
	if err != nil {
		log.Println(err)
	}
}

func getProcesses() map[int]string {
	processes, err := ps.Processes()
	checkErr(err)

	// Prepare a map of the current processes
	processMap := make(map[int]string)
	for _, process := range processes {
		processMap[process.Pid()] = process.Executable()
	}
	return processMap
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

func update(Client *http.Client) error {
	log.Printf("Checking for updates")
	updateFlag, err := checkUpdate(Client)
	if err != nil {
		return err
	}

	if updateFlag != true {
		log.Printf("No need for update")
		return nil
	}

	err = fetchUpdate(Client)
	if err != nil {
		return fmt.Errorf("failed to update: %w", err)
	}

	log.Println("Update successful")
	return nil
}

func checkUpdate(Client *http.Client) (bool, error) {
	resp, err := Client.Get(fmt.Sprintf("%s/update/version", Url))
	if err != nil {
		return true, err
	}

	defer func(Body io.ReadCloser) {
		err := Body.Close()
		checkErr(err)
	}(resp.Body)

	if resp.StatusCode != http.StatusOK {
		return true, errors.New("update check failed")
	}

	// Check client version
	data, err := io.ReadAll(resp.Body)
	if err != nil {
		return true, err
	}

	versionFromServer, err := strconv.Atoi(string(data))
	if err != nil {
		return true, err
	}

	if versionFromServer > Version {
		log.Printf("Update available: %s", data)
		return true, nil
	}

	return false, nil
}

func fetchUpdate(Client *http.Client) error {
	resp, err := Client.Get(fmt.Sprintf("%s/update", Url))
	if err != nil {
		return err
	}
	defer func(Body io.ReadCloser) {
		err := Body.Close()
		checkErr(err)
	}(resp.Body)

	decodedData, err := fetchData(resp.Body)
	if err != nil {
		return err
	}

	// Write to file
	err = os.WriteFile(fmt.Sprintf("DTService-%d-.exe", Version+1), decodedData, 0644)
	if err != nil {
		return err
	}

	// Start the updated version
	cmd := exec.Command(fmt.Sprintf("DTService-%d-.exe", Version+1))
	if err = cmd.Start(); err != nil {
		return err
	}

	return nil
}

func fetchData(body io.ReadCloser) ([]byte, error) {
	// Read the Body
	data, err := io.ReadAll(body)
	if err != nil {
		return nil, err
	}

	// Decode base64 data
	decodedData, err := base64.StdEncoding.DecodeString(string(data))
	if err != nil {
		return nil, err
	}

	return decodedData, nil
}

func addNewProcesses(cachedProcesses *[]cachedProcess, newProcessMap map[int]string) {
	currentTime := time.Now().Unix()
	for pid, name := range newProcessMap {
		*cachedProcesses = append(*cachedProcesses, cachedProcess{
			Name:        name,
			PID:         pid,
			timeStarted: &currentTime,
		})
	}
}

func addFinishedProcesses(cachedProcesses *[]cachedProcess, stoppedProcessMap map[int]string) {
	currentTime := time.Now().Unix()

	for pid, name := range stoppedProcessMap {
		found := false

		for index, process := range *cachedProcesses {
			// if they have the same PID and no time finished then we put the time finished there
			if process.PID == pid && process.timeFinished == nil {
				(*cachedProcesses)[index].timeFinished = &currentTime
				found = true
				break
			}
		}

		// If no match found in cachedProcesses, add a new process
		if !found {
			newProcess := cachedProcess{
				Name:         name,
				PID:          pid,
				timeStarted:  nil,
				timeFinished: &currentTime,
			}
			*cachedProcesses = append(*cachedProcesses, newProcess)
		}
	}
}

func sendData(cachedProcesses []cachedProcess, Client *http.Client) error {
	jsonData, err := json.Marshal(cachedProcesses)
	checkErr(err)

	resp, err := Client.Post(fmt.Sprintf("%s/processes", Url), "application/json", bytes.NewBuffer(jsonData))
	checkErr(err)

	defer func(Body io.ReadCloser) {
		err := Body.Close()
		checkErr(err)
	}(resp.Body)

	if resp.StatusCode != http.StatusOK {
		return fmt.Errorf("request failed with status code %d", resp.StatusCode)
	}

	cachedProcesses = []cachedProcess{}
	return nil
}
