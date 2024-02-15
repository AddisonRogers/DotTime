package main

import (
	"encoding/json"
	"fmt"
	"log"
	"net/http"
)

type cachedProcess struct {
	Name         string
	PID          int
	timeStarted  *int64
	timeFinished *int64
}

func main() {
	serverMux := http.NewServeMux()

	serverMux.HandleFunc("/", func(w http.ResponseWriter, r *http.Request) {
		_, err := fmt.Fprintf(w, "Hello, you've requested: %s\n", r.URL.Path)
		if err != nil {
			log.Fatal(err)
		}
	})

	serverMux.HandleFunc("/process", func(w http.ResponseWriter, r *http.Request) {
		// Insert code here to handle the /process endpoint
		w.WriteHeader(http.StatusOK)
		_, err := w.Write([]byte("Processing..."))
		if err != nil {
			log.Fatal(err)
		}
		if r.Method != http.MethodPost {
			_, err := fmt.Fprintf(w, "This does not return any value")
			if err != nil {
				log.Fatal(err)
			}
			return
		}

		// we need to convert this to a slice of cachedProcess
		var processes []cachedProcess
		err = json.NewDecoder(r.Body).Decode(&processes)
		if err != nil {
			http.Error(w, err.Error(), http.StatusBadRequest)
			return
		}

		// We need postgres

		for _, process := range processes {
			// process each cachedProcess here
		}
	})

	fmt.Println("Listening on port 8080...")
	err := http.ListenAndServe(":8080", serverMux)
	if err != nil {
		log.Fatal(err)
	}
}
