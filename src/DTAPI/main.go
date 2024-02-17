package main

import (
	"encoding/base64"
	"encoding/json"
	"fmt"
	"gorm.io/driver/postgres"
	"gorm.io/gorm"
	"log"
	"net/http"
	_ "net/http/pprof"
	"os"
	"path/filepath"
	"strconv"
)

type processes struct {
	Name         string `json:"name"`
	TimeStarted  int64  `json:"timeStarted"`
	TimeFinished *int64 `json:"timeFinished"`
}

type rawJson struct {
	Token     string      `json:"token"`
	Processes []processes `json:"processes"`
}

const Version int = 1

func main() {
	serverMux := http.NewServeMux()
	dsn := "host=localhost user=postgres password=mysecretpassword dbname=postgres port=5432"
	db, err := gorm.Open(postgres.Open(dsn), &gorm.Config{})
	if err == nil {
		log.Println("Database Connected")
	} else {
		log.Fatal(err)
	}

	serverMux.HandleFunc("GET /", func(w http.ResponseWriter, r *http.Request) {
		_, err := fmt.Fprintf(w, "Hello, you've requested: %s\n", r.URL.Path)
		if err != nil {
			log.Fatal(err)
		}
	})

	serverMux.HandleFunc("GET /version", func(w http.ResponseWriter, r *http.Request) {
		_, err := fmt.Fprintf(w, strconv.Itoa(Version))
		if err != nil {
			log.Fatal(err)
		}
	})

	serverMux.HandleFunc("POST /process", func(w http.ResponseWriter, r *http.Request) {
		var jsonStuff rawJson
		var processPlural []processes
		err = json.NewDecoder(r.Body).Decode(&jsonStuff)
		if err != nil {
			http.Error(w, err.Error(), http.StatusBadRequest)
			return
		}

		processPlural = jsonStuff.Processes

		var existingProcesses []processes
		err = db.Where("Token = ?", jsonStuff.Token).Find(&existingProcesses).Error
		if err != nil {
			db.Rollback()
			http.Error(w, err.Error(), http.StatusInternalServerError)
			return
		}

		tx := db.Begin()
		for _, process := range processPlural {
			if process.TimeFinished == nil {
				var queriedProcesses []processes // All the values that can match to it
				tx.Where("Name = ? AND TimeFinished IS NULL", process.Name).FirstOrCreate(&queriedProcesses, &process)
				if len(queriedProcesses) == 0 {
					err = tx.Create(&process).Error
					if err != nil {
						tx.Rollback()
						http.Error(w, err.Error(), http.StatusInternalServerError)
						return
					}
				} else {
					err = tx.Model(&queriedProcesses[0]).Updates(process).Error
					if err != nil {
						tx.Rollback()
						http.Error(w, err.Error(), http.StatusInternalServerError)
						return
					}
				}
			} else { // This just creates the thing
				err = tx.Create(&process).Error
				if err != nil {
					tx.Rollback()
					http.Error(w, err.Error(), http.StatusInternalServerError)
					return
				}
			}
		}
		tx.Commit()
	})

	serverMux.HandleFunc("GET /update", func(w http.ResponseWriter, r *http.Request) {
		queryParams := r.URL.Query()

		architecture := queryParams.Get("architecture")
		if architecture == "" {
			architecture = "x86_64"
		}

		system := queryParams.Get("system")
		if system == "" {
			system = "windows"
		}

		dir, err := os.Getwd()
		if err != nil {
			log.Fatal(err)
			return
		}

		data, err := os.ReadFile(filepath.Join(dir, "/files", system+"-"+architecture+".bytes"))
		if err != nil {
			http.Error(w, err.Error(), http.StatusInternalServerError)
			return
		}

		_, err = w.Write([]byte(base64.StdEncoding.EncodeToString(data)))
		if err != nil {
			log.Fatal(err)
			return
		}
	})

	log.Println("Listening on port 8080...")
	err = http.ListenAndServe(":8080", serverMux)
	if err != nil {
		log.Fatal(err)
	}
}
