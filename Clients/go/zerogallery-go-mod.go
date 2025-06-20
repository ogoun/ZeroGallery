module github.com/yourusername/zerogallery

go 1.21

require (
	// Стандартная библиотека Go содержит все необходимое
	// Дополнительные зависимости не требуются
)

// Опциональные зависимости для расширенной функциональности:
// 
// Для retry логики:
// require github.com/avast/retry-go/v4 v4.5.1
//
// Для продвинутого логирования:
// require go.uber.org/zap v1.26.0
//
// Для работы с конфигурацией:
// require github.com/spf13/viper v1.18.2
//
// Для CLI интерфейса:
// require github.com/spf13/cobra v1.8.0