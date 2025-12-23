CREATE TABLE IF NOT EXISTS tts_jobs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    job_id VARCHAR(255) UNIQUE NOT NULL,
    workout_id UUID NOT NULL,
    language VARCHAR(10) DEFAULT 'ru-RU',
    voice_profile VARCHAR(100) DEFAULT 'en-US-alina',
    payload JSONB,
    status VARCHAR(50) DEFAULT 'pending',
    result_url TEXT,
    error_message TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_tts_jobs_job_id ON tts_jobs(job_id);
CREATE INDEX idx_tts_jobs_workout_id ON tts_jobs(workout_id);
CREATE INDEX idx_tts_jobs_status ON tts_jobs(status);
-- Добавляем архивную таблицу
CREATE TABLE IF NOT EXISTS tts_messages_archive (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    queue_name VARCHAR(100) NOT NULL,
    message_id VARCHAR(100) NOT NULL,
    message_body JSONB NOT NULL,
    job_id VARCHAR(100),
    processed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_tts_archive_job_id ON tts_messages_archive(job_id);
CREATE INDEX IF NOT EXISTS idx_tts_archive_queue ON tts_messages_archive(queue_name);