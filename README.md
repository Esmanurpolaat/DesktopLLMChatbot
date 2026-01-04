# DesktopLLMChatbot
# DesktopLLMChatbot

Bu proje, C# Windows Forms kullanılarak geliştirilmiş bir masaüstü sohbet uygulamasıdır.
Uygulama, bir Büyük Dil Modeli (LLM) ile HTTP üzerinden haberleşerek kullanıcı girdilerine
doğal dilde yanıt üretir.

## Kullanılan Teknolojiler
- C#
- .NET (Windows Forms)
- HTTP tabanlı LLM entegrasyonu
- Streaming (parça parça yanıt alma) yaklaşımı

## Proje Yapısı
- Tek bir Windows Form üzerinden çalışan arayüz
- Kullanıcı girdisi → LLM isteği → yanıt akışı mantığı
- Sohbet geçmişi (context) tutulmaktadır

## LLM Entegrasyonu
Bu projede, geliştirme ve test amacıyla **Ollama** kullanılarak lokal bir LLM entegrasyonu yapılmıştır.

> Not: Ollama yalnızca ücretsiz test ve geliştirme amacıyla tercih edilmiştir.  
> Kod yapısı, OpenAI veya Azure OpenAI gibi bulut tabanlı LLM API’lerine
> kolayca uyarlanabilecek şekilde tasarlanmıştır.

## Çalıştırma
Uygulamanın LLM yanıtı üretebilmesi için:
- Ollama’nın kurulu olması
- En az bir modelin (ör. llama3.1) indirilmiş olması gerekir

LLM kurulumu olmayan ortamlarda uygulama arayüzü ve kod yapısı incelenebilir.

## Amaç
Bu projenin amacı:
- Masaüstü uygulamalarda LLM entegrasyonunun nasıl yapılabileceğini göstermek
- Prompt gönderimi, yanıt işleme ve streaming cevap alma süreçlerini örneklemektir
