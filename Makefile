up:
	docker-compose up -d

down:
	docker-compose down

logs:
	docker-compose logs -f

test:
	curl http://localhost:8000/health && echo
	curl http://localhost:8001/health && echo
	curl http://localhost:8002/health && echo

db-shell:
	docker-compose exec training-db psql -U postgres -d training_db

rabbitmq:
	open http://localhost:15672 || echo "Open http://localhost:15672"

